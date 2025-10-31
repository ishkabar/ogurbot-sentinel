using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Ogur.Sentinel.Desktop.Config;
using Ogur.Sentinel.Desktop.Services;

namespace Ogur.Sentinel.Desktop.Views;

public partial class TimerView : Page
{
    private readonly ApiClient _apiClient;
    private readonly MainWindow _mainWindow;
    private readonly DesktopSettings _settings;
    private readonly DispatcherTimer? _countdownTimer;
    private readonly DispatcherTimer _syncTimer;
    private DispatcherTimer? _resizeDebounceTimer;

    private DateTime? _next10m;
    private DateTime? _next2h;
    private DateTime? _actualNext10m;  
    private DateTime? _actualNext2h;  


    public TimerView(ApiClient apiClient, MainWindow mainWindow, DesktopSettings settings)
    {
        InitializeComponent();

        _apiClient = apiClient;
        _mainWindow = mainWindow;
        _settings = settings;

        // ❌ USUŃ te linie - MinWidth/MinHeight ustawia MainWindow.NavigateToTimers()
        // _mainWindow.MinWidth = 130;
        // _mainWindow.MinHeight = 100;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(settings.SyncIntervalSeconds)
        };
        _syncTimer.Tick += async (s, e) => await SyncWithApi();
        _syncTimer.Start();

        _resizeDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _resizeDebounceTimer.Tick += (s, e) =>
        {
            _resizeDebounceTimer.Stop();
            // ✅ Loguj rozmiar po zakończeniu resize
            Console.WriteLine($"📏 Final size: Width={ActualWidth:F0}, Height={ActualHeight:F0}");
        };

        Loaded += async (s, e) => await SyncWithApi();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        AdaptLayout(e.NewSize.Width, e.NewSize.Height);

        _resizeDebounceTimer?.Stop();
        _resizeDebounceTimer?.Start();
    }

    private void AdaptLayout(double width, double height)
    {
        TimersContainer.RowDefinitions.Clear();
        TimersContainer.ColumnDefinitions.Clear();

        // Marginesy kontenera - uwzględniamy też wysokość dla pionowego layoutu
        Thickness containerMargin;
        Thickness scrollPadding;

        bool isHorizontal = width > height * 1.2;

        // ✅ Dla pionowego layoutu przy małych wysokościach - jeszcze mniejsze marginesy
        if (!isHorizontal && height < 140)
        {
            containerMargin = new Thickness(1);
            scrollPadding = new Thickness(1);
        }
        else if (width < 70)
        {
            containerMargin = new Thickness(1);
            scrollPadding = new Thickness(1);
        }
        else if (width < 120)
        {
            containerMargin = new Thickness(3);
            scrollPadding = new Thickness(2);
        }
        else if (width < 200)
        {
            containerMargin = new Thickness(5);
            scrollPadding = new Thickness(5);
        }
        else if (width < 400)
        {
            containerMargin = new Thickness(8);
            scrollPadding = new Thickness(8);
        }
        else
        {
            containerMargin = new Thickness(12);
            scrollPadding = new Thickness(10);
        }

        //MainScrollViewer.Padding = scrollPadding;
        TimersContainer.Margin = containerMargin;


        if (isHorizontal && width > 350)
        {
            // ======= LAYOUT POZIOMY =======
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TimersContainer.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            TimersContainer.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(HeaderText, 0);
            Grid.SetColumn(HeaderText, 0);
            Grid.SetColumnSpan(HeaderText, 2);
            HeaderText.Margin = new Thickness(0); // ✅ Usuń margin

            Grid.SetRow(Timer10mBorder, 1);
            Grid.SetColumn(Timer10mBorder, 0);
            Timer10mBorder.VerticalAlignment = VerticalAlignment.Center;
            Timer10mBorder.HorizontalAlignment = HorizontalAlignment.Stretch; // ✅ Stretch

            var gapMargin = width < 400 ? 3 : 5;
            Timer10mBorder.Margin = new Thickness(0, 0, gapMargin, 0);

            Grid.SetRow(Timer2hBorder, 1);
            Grid.SetColumn(Timer2hBorder, 1);
            Timer2hBorder.VerticalAlignment = VerticalAlignment.Center;
            Timer2hBorder.HorizontalAlignment = HorizontalAlignment.Stretch; // ✅ Stretch
            Timer2hBorder.Margin = new Thickness(gapMargin, 0, 0, 0);

            Grid.SetRow(StatusText, 2);
            Grid.SetColumn(StatusText, 0);
            Grid.SetColumnSpan(StatusText, 2);
            StatusText.Margin = new Thickness(0, 5, 0, 0);

            AdjustFontSizes(width / 2, height);
        }
        else
        {
            // ======= LAYOUT PIONOWY =======
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TimersContainer.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(HeaderText, 0);
            Grid.SetColumn(HeaderText, 0);
            // ✅ Mniejszy margin dla małych wysokości
            HeaderText.Margin = height < 140 ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 0, 5);

            Grid.SetRow(Timer10mBorder, 1);
            Grid.SetColumn(Timer10mBorder, 0);
            Timer10mBorder.VerticalAlignment = VerticalAlignment.Top;
            Timer10mBorder.HorizontalAlignment = HorizontalAlignment.Stretch; // ✅ Stretch

            // ✅ Bardziej agresywne zmniejszanie vertical gap dla małych wysokości
            var verticalGap = height < 100 ? 1 : (height < 140 ? 2 : (height < 200 ? 3 : (height < 400 ? 5 : 8)));
            Timer10mBorder.Margin = new Thickness(0, 0, 0, verticalGap);

            Grid.SetRow(Timer2hBorder, 2);
            Grid.SetColumn(Timer2hBorder, 0);
            Timer2hBorder.VerticalAlignment = VerticalAlignment.Top;
            Timer2hBorder.HorizontalAlignment = HorizontalAlignment.Stretch; // ✅ Stretch
            Timer2hBorder.Margin = new Thickness(0, 0, 0, 0);

            Grid.SetRow(StatusText, 3);
            Grid.SetColumn(StatusText, 0);
            StatusText.Margin = new Thickness(0, verticalGap, 0, 0);

            AdjustFontSizes(width, height);
        }
    }

    private void AdjustFontSizes(double availableWidth, double availableHeight)
    {
        double countdownSize = 48;
        double labelSize = 16;
        double nextTimeSize = 12;
        double headerSize = 24;
        bool showLabels = true;
        bool showNextTime = true;
        bool showHeader = true;
        bool showStatus = true;

        // Sprawdź czy jesteśmy w trybie poziomym
        bool isHorizontal = ActualWidth > ActualHeight * 1.2 && ActualWidth > 350;

        // POZIOMY LAYOUT
        if (isHorizontal)
        {
            // ✅ ZAWSZE ukryj header w poziomym
            showHeader = false;

            if (availableHeight < 60)
            {
                // Ultra ultra mała wysokość - TYLKO countdown, małe czcionki
                countdownSize = Math.Max(10, availableHeight * 0.55);
                labelSize = 2;
                nextTimeSize = 7;
                showLabels = false;
                showNextTime = false;
                showStatus = false;
            }
            else if (availableHeight < 80)
            {
                // Ultra ultra mała wysokość - TYLKO countdown, małe czcionki
                countdownSize = Math.Max(12, availableHeight * 0.55);
                labelSize = 1;
                nextTimeSize = 4;
                showLabels = false;
                showNextTime = false;
                showStatus = false;
            }
            else if (availableHeight < 100)
            {
                // Ultra mała wysokość - TYLKO countdown
                countdownSize = Math.Max(24, availableHeight * 0.6);
                labelSize = 10;
                nextTimeSize = 8;
                showLabels = false;
                showNextTime = false;
                showStatus = false;
            }
            else if (availableHeight < 140)
            {
                // Mała wysokość - countdown (bez labels i next)
                countdownSize = Math.Max(32, availableHeight * 0.5);
                labelSize = 11;
                nextTimeSize = 9;
                showLabels = false; // ✅ Nadal ukryte
                showNextTime = false;
                showStatus = false;
            }
            else if (availableHeight < 180)
            {
                // Średnia wysokość - countdown + labels
                countdownSize = Math.Max(40, availableHeight * 0.4);
                labelSize = 13;
                nextTimeSize = 10;
                showLabels = true; // ✅ Pokaż labels
                showNextTime = false;
                showStatus = false;
            }
            else if (availableHeight < 220)
            {
                // Większa wysokość - countdown + labels + next
                countdownSize = Math.Max(48, availableHeight * 0.35);
                labelSize = 15;
                nextTimeSize = 11;
                showLabels = true;
                showNextTime = true; // ✅ Pokaż next time
                showStatus = false;
            }
            else
            {
                // Pełna wysokość - wszystko
                countdownSize = 56;
                labelSize = 17;
                nextTimeSize = 12;
                showLabels = true;
                showNextTime = true;
                showStatus = true;
            }
        }
        // PIONOWY LAYOUT
        else
        {
            if (availableWidth < 70)
            {
                countdownSize = Math.Max(12, availableWidth * 0.6);
                labelSize = 8;
                nextTimeSize = 6;
                headerSize = 10;
                showLabels = false;
                showNextTime = false;
                showHeader = false;
                showStatus = false;
            }
            else if (availableWidth < 100)
            {
                // ✅ Uwzględnij wysokość dla bardzo małych okien
                if (availableHeight < 100)
                {
                    countdownSize = Math.Max(18, availableWidth * 0.45);
                    labelSize = 8;
                    nextTimeSize = 7;
                    headerSize = 10;
                }
                else if (availableHeight < 140)
                {
                    countdownSize = Math.Max(20, availableWidth * 0.48);
                    labelSize = 9;
                    nextTimeSize = 7;
                    headerSize = 12;
                }
                else
                {
                    countdownSize = Math.Max(20, availableWidth * 0.5);
                    labelSize = 10;
                    nextTimeSize = 8;
                    headerSize = 14;
                }

                showLabels = false;
                showNextTime = false;
                showHeader = false;
                showStatus = false;
            }
            else if (availableWidth < 150)
            {
                // ✅ Uwzględnij wysokość
                if (availableHeight < 100)
                {
                    countdownSize = 22;
                    labelSize = 8;
                    nextTimeSize = 7;
                    headerSize = 11;
                    showLabels = false;
                    showNextTime = false;
                    showHeader = false;
                    showStatus = false;
                }
                else if (availableHeight < 140)
                {
                    countdownSize = 26;
                    labelSize = 10;
                    nextTimeSize = 8;
                    headerSize = 12;
                    showLabels = false;
                    showNextTime = false;
                    showHeader = false;
                    showStatus = false;
                }
                else
                {
                    countdownSize = 32;
                    labelSize = 12;
                    nextTimeSize = 9;
                    headerSize = 18;
                    showLabels = false;
                    showNextTime = false;
                    showHeader = true;
                    showStatus = false;
                }
            }
            else if (availableWidth < 200)
            {
                // ✅ Uwzględnij wysokość
                if (availableHeight < 100)
                {
                    countdownSize = 28;
                    labelSize = 10;
                    nextTimeSize = 8;
                    headerSize = 13;
                    showLabels = false;
                    showNextTime = false;
                    showHeader = false;
                    showStatus = false;
                }
                else if (availableHeight < 140)
                {
                    countdownSize = 32;
                    labelSize = 12;
                    nextTimeSize = 9;
                    headerSize = 15;
                    showLabels = true;
                    showNextTime = false;
                    showHeader = false;
                    showStatus = false;
                }
                else
                {
                    countdownSize = 40;
                    labelSize = 14;
                    nextTimeSize = 10;
                    headerSize = 20;
                    showLabels = true;
                    showNextTime = false;
                }
            }
            else if (availableWidth < 300)
            {
                countdownSize = 48;
                labelSize = 16;
                nextTimeSize = 11;
                headerSize = 22;
            }
            else if (availableWidth < 450)
            {
                countdownSize = 56;
                labelSize = 17;
                nextTimeSize = 12;
                headerSize = 24;
            }
            else
            {
                countdownSize = 72;
                labelSize = 20;
                nextTimeSize = 14;
                headerSize = 28;
            }
        }

        // ✅ Zastosuj rozmiary countdown
        Countdown10mText.FontSize = countdownSize;
        Countdown2hText.FontSize = countdownSize;
        Countdown10mText.Margin = new Thickness(0);
        Countdown2hText.Margin = new Thickness(0);

        // ✅ Labels - z kontrolą wysokości
        if (showLabels)
        {
            Label10m.Visibility = Visibility.Visible;
            Label2h.Visibility = Visibility.Visible;
            Label10m.FontSize = labelSize;
            Label2h.FontSize = labelSize;

            // Marginesy zależne od wysokości
            var labelMargin = availableHeight < 150 ? new Thickness(0, 1, 0, 1) : new Thickness(0, 3, 0, 3);
            Label10m.Margin = labelMargin;
            Label2h.Margin = labelMargin;
        }
        else
        {
            Label10m.Visibility = Visibility.Collapsed;
            Label2h.Visibility = Visibility.Collapsed;
        }

        // ✅ Next Time - z kontrolą wysokości
        if (showNextTime)
        {
            NextTime10mText.Visibility = Visibility.Visible;
            NextTime2hText.Visibility = Visibility.Visible;
            NextTime10mText.FontSize = nextTimeSize;
            NextTime2hText.FontSize = nextTimeSize;

            var nextMargin = availableHeight < 150 ? new Thickness(0, 1, 0, 1) : new Thickness(0, 3, 0, 3);
            NextTime10mText.Margin = nextMargin;
            NextTime2hText.Margin = nextMargin;
        }
        else
        {
            NextTime10mText.Visibility = Visibility.Collapsed;
            NextTime2hText.Visibility = Visibility.Collapsed;
        }

        // ✅ Header
        if (showHeader)
        {
            HeaderText.Visibility = Visibility.Visible;
            HeaderText.FontSize = headerSize;
        }
        else
        {
            HeaderText.Visibility = Visibility.Collapsed;
        }

        // ✅ Status
        if (showStatus)
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.FontSize = availableWidth < 200 ? 10 : 12;
        }
        else
        {
            StatusText.Visibility = Visibility.Collapsed;
        }

        // ✅ Padding w boxach - MNIEJSZY w poziomym i przy małych wysokościach pionowych
        Thickness boxPadding;
        if (isHorizontal)
        {
            if (availableHeight < 80)
                boxPadding = new Thickness(2);
            else if (availableHeight < 150)
                boxPadding = new Thickness(4);
            else
                boxPadding = new Thickness(6);
        }
        else
        {
            // ✅ Pionowy - uwzględniamy zarówno szerokość jak i wysokość
            if (availableHeight < 100 || availableWidth < 80)
                boxPadding = new Thickness(2);
            else if (availableHeight < 140 || availableWidth < 120)
                boxPadding = new Thickness(3);
            else if (availableWidth < 150)
                boxPadding = new Thickness(5);
            else if (availableWidth < 300)
                boxPadding = new Thickness(8);
            else
                boxPadding = new Thickness(12);
        }

        Timer10mBorder.Padding = boxPadding;
        Timer2hBorder.Padding = boxPadding;

        // ✅ Przyciski
        if (availableWidth < 150)
        {
            //SyncButton.Content = "🔄";
            //LogoutButton.Content = "🚪";
        }
        else
        {
            //SyncButton.Content = "🔄 Sync";
            //LogoutButton.Content = "🚪 Logout";
        }
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        UpdateCountdowns();
    }


private void UpdateCountdowns()
{
    var now = DateTime.Now;

    // ✅ Countdown używa PRAWDZIWEGO czasu (bez offsetu)
    if (_actualNext10m.HasValue)
    {
        var remaining = _actualNext10m.Value - now;

        if (remaining.TotalSeconds > 10) // ✅ Pokaż countdown dopóki > 10s
        {
            Countdown10mText.Text = FormatTimeSpan(remaining);
            Countdown10mText.Foreground = GetColorForTimeRemaining(remaining);
        }
        else if (remaining.TotalSeconds > 0)
        {
            // 0-10 sekund przed respem - pokaż czerwony countdown
            Countdown10mText.Text = FormatTimeSpan(remaining);
            Countdown10mText.Foreground = new SolidColorBrush(Colors.Red);
        }
        else if (remaining.TotalSeconds > -10)
        {
            // 0-10 sekund PO respie - RESPAWN!
            Countdown10mText.Text = "RESPAWN!";
            Countdown10mText.Foreground = new SolidColorBrush(Colors.Red);
        }
        else
        {
            // > 10s po respie - czekaj na sync z API
            Countdown10mText.Text = "Syncing...";
            Countdown10mText.Foreground = new SolidColorBrush(Colors.Orange);
        }
    }
    else
    {
        Countdown10mText.Text = "--:--:--";
        Countdown10mText.Foreground = new SolidColorBrush(Colors.White);
    }

    // To samo dla 2h
    if (_actualNext2h.HasValue)
    {
        var remaining = _actualNext2h.Value - now;

        if (remaining.TotalSeconds > 10)
        {
            Countdown2hText.Text = FormatTimeSpan(remaining);
            Countdown2hText.Foreground = GetColorForTimeRemaining(remaining);
        }
        else if (remaining.TotalSeconds > 0)
        {
            Countdown2hText.Text = FormatTimeSpan(remaining);
            Countdown2hText.Foreground = new SolidColorBrush(Colors.Red);
        }
        else if (remaining.TotalSeconds > -10)
        {
            Countdown2hText.Text = "RESPAWN!";
            Countdown2hText.Foreground = new SolidColorBrush(Colors.Red);
        }
        else
        {
            Countdown2hText.Text = "Syncing...";
            Countdown2hText.Foreground = new SolidColorBrush(Colors.Orange);
        }
    }
    else
    {
        Countdown2hText.Text = "--:--:--";
        Countdown2hText.Foreground = new SolidColorBrush(Colors.White);
    }
}

private async Task SyncWithApi()
{
    try
    {
        var times = await _apiClient.GetNextRespawnAsync();

        if (times != null)
        {
            // ✅ Zapisz PRAWDZIWY czas (do countdown)
            _actualNext10m = times.Next10m.ToLocalTime();
            _actualNext2h = times.Next2h.ToLocalTime();
            
            // ✅ Oblicz WYŚWIETLANY czas z offsetem (tylko do pokazania "Next:")
            var displayNext10m = _actualNext10m.Value.AddSeconds(-_settings.TimeOffsetSeconds);
            var displayNext2h = _actualNext2h.Value.AddSeconds(-_settings.TimeOffsetSeconds);

            // ✅ Pokaż czas Z OFFSETEM w "Next:"
            NextTime10mText.Text = $"Next: {displayNext10m:HH:mm:ss}";
            NextTime2hText.Text = $"Next: {displayNext2h:HH:mm:ss}";

            StatusText.Text = $"Synced: {DateTime.Now:HH:mm:ss}";
            StatusText.Foreground = new SolidColorBrush(Colors.Green);

            UpdateCountdowns();
        }
        else
        {
            StatusText.Text = "Sync failed";
            StatusText.Foreground = new SolidColorBrush(Colors.Red);
        }
    }
    catch
    {
        StatusText.Text = "Error";
        StatusText.Foreground = new SolidColorBrush(Colors.Red);
    }
}

private string FormatTimeSpan(TimeSpan ts)
{
    if (ts.TotalHours >= 1)
    {
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
    else
    {
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}

private Brush GetColorForTimeRemaining(TimeSpan remaining)
{
    if (remaining.TotalMinutes < _settings.WarningMinutesRed)
    {
        return new SolidColorBrush(Colors.Red);
    }
    else if (remaining.TotalMinutes < _settings.WarningMinutesOrange)
    {
        return new SolidColorBrush(Colors.Orange);
    }
    else
    {
        return new SolidColorBrush(Colors.White);
    }
}
}