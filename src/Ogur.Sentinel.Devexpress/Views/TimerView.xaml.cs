using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Ogur.Sentinel.Devexpress.ViewModels;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.Views
{
    public partial class TimerView : Page
    {
        private readonly TimerViewModel _viewModel;
        private DispatcherTimer _resizeDebounceTimer;
        private bool _labelsVisible = true;
        private bool _nextTimeVisible = true;
        private bool _headerVisible = true;
        private bool _isHorizontal = false;

        public TimerView(ApiClient apiClient, DesktopSettings settings)
        {
            InitializeComponent();

            _viewModel = new TimerViewModel(apiClient, settings);
            DataContext = _viewModel;

            _resizeDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
    
            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
            };

            // ✅ Zainicjalizuj layout po załadowaniu
            this.Loaded += (s, e) =>
            {
                // Ustaw domyślny layout pionowy
                InitializeLayout();
        
                // Wywołaj adapt z aktualnymi wymiarami
                if (ActualWidth > 0 && ActualHeight > 0)
                {
                    AdaptLayout(ActualWidth, ActualHeight);
                }
            };
        }

        private void InitializeLayout()
        {
            // Domyślnie układ pionowy
            TimersContainer.RowDefinitions.Clear();
            TimersContainer.ColumnDefinitions.Clear();
    
            TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(Timer10mBorder, 0);
            Grid.SetColumn(Timer10mBorder, 0);
            Grid.SetRow(Timer2hBorder, 1);
            Grid.SetColumn(Timer2hBorder, 0);
    
            _isHorizontal = false;
    
            Console.WriteLine("🎬 [TimerView] Layout initialized (vertical)");
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdaptLayout(e.NewSize.Width, e.NewSize.Height);
            
            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer?.Start();
        }
        
        private void AdaptLayout(double width, double height)
        {
            // Header visibility z animacją
            bool shouldShowHeader = height >= 150;
            if (shouldShowHeader != _headerVisible)
            {
                AnimateHeaderVisibility(shouldShowHeader);
                _headerVisible = shouldShowHeader;
        
                // ✅ Po zmianie headera poczekaj chwilę i przelicz layout
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Wymuś ponowne obliczenie layoutu
                    TimersContainer.InvalidateMeasure();
                    TimersContainer.UpdateLayout();
            
                    // Przelicz wysokość kontenera
                    double containerHeight = _isHorizontal ? TimersContainer.ActualHeight : TimersContainer.ActualHeight / 2;
                    AdaptTimerElements(containerHeight);
                    AdjustFontSizes(width, height, _isHorizontal, containerHeight);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
    
            // Dostosuj margines głównego grida
            MainGrid.Margin = height < 150 ? new Thickness(5) : new Thickness(20);

            // ✅ Oblicz proporcję width/height
            double aspectRatio = width / height;
            bool shouldBeHorizontal = aspectRatio >= 0.7; // Próg 70/30

            // Animuj zmianę orientacji tylko gdy się zmienia
            if (shouldBeHorizontal != _isHorizontal)
            {
                AnimateOrientationChange(shouldBeHorizontal);
                _isHorizontal = shouldBeHorizontal;
            }

            double containerHeight = _isHorizontal ? TimersContainer.ActualHeight : TimersContainer.ActualHeight / 2;
    
            // Adaptuj elementy z animacją
            AdaptTimerElements(containerHeight);
    
            // Dostosuj rozmiary czcionek
            AdjustFontSizes(width, height, _isHorizontal, containerHeight);
        }
        
        private void AnimateHeaderVisibility(bool show)
        {
            var scaleAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            HeaderScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            HeaderText.BeginAnimation(OpacityProperty, opacityAnim);
        }

        private void AnimateOrientationChange(bool toHorizontal)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                // Zmień layout
                TimersContainer.RowDefinitions.Clear();
                TimersContainer.ColumnDefinitions.Clear();

                if (toHorizontal)
                {
                    // Layout poziomy
                    TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(Timer10mBorder, 0);
                    Grid.SetColumn(Timer10mBorder, 0);
                    Grid.SetRow(Timer2hBorder, 0);
                    Grid.SetColumn(Timer2hBorder, 1);
                }
                else
                {
                    // Layout pionowy
                    TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(Timer10mBorder, 0);
                    Grid.SetColumn(Timer10mBorder, 0);
                    Grid.SetRow(Timer2hBorder, 1);
                    Grid.SetColumn(Timer2hBorder, 0);
                }

                // Fade in
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                TimersContainer.BeginAnimation(OpacityProperty, fadeIn);
            };

            TimersContainer.BeginAnimation(OpacityProperty, fadeOut);
        }
        
        private void AdaptTimerElements(double containerHeight)
{
    // ✅ Labels - pojawiają się od wysokości 100 (było 150)
    bool shouldShowLabels = containerHeight >= 100;
    if (shouldShowLabels != _labelsVisible)
    {
        AnimateElementVisibility(Label10mScale, Label10m, shouldShowLabels);
        AnimateElementVisibility(Label2hScale, Label2h, shouldShowLabels);
        _labelsVisible = shouldShowLabels;
    }

    // ✅ Next Time - pojawiają się od wysokości 60 (było 80)
    bool shouldShowNextTime = containerHeight >= 60;
    if (shouldShowNextTime != _nextTimeVisible)
    {
        AnimateElementVisibility(NextTime10mScale, NextTime10mText, shouldShowNextTime);
        AnimateElementVisibility(NextTime2hScale, NextTime2hText, shouldShowNextTime);
        _nextTimeVisible = shouldShowNextTime;
    }
}

        private void AnimateElementVisibility(System.Windows.Media.ScaleTransform scale, FrameworkElement element, bool show)
        {
            var scaleAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            element.BeginAnimation(OpacityProperty, opacityAnim);
        }

       private void AdjustFontSizes(double availableWidth, double availableHeight, bool isHorizontal, double containerHeight)
{
    double countdownSize, nextTimeSize, labelSize;
    double borderMargin, borderPadding;

    if (isHorizontal)
    {
        double panelWidth = availableWidth / 2;
        double minDimension = Math.Min(panelWidth, availableHeight);

        countdownSize = Math.Max(16, Math.Min(72, minDimension * 0.20));
        nextTimeSize = Math.Max(8, Math.Min(16, minDimension * 0.04));
        labelSize = Math.Max(10, Math.Min(18, minDimension * 0.045));
        
        // Marginesy i padding proporcjonalne do rozmiaru
        borderMargin = Math.Max(2, Math.Min(8, minDimension * 0.015));
        borderPadding = Math.Max(3, Math.Min(15, minDimension * 0.03));
    }
    else
    {
        double panelHeight = availableHeight / 2;
        double minDimension = Math.Min(availableWidth, panelHeight);

        // Gdy elementy są ukryte, countdown może być większy
        if (containerHeight < 150)
        {
            countdownSize = Math.Max(20, Math.Min(80, minDimension * 0.40));
            // Mniejsze marginesy gdy countdown jest duży
            borderMargin = Math.Max(1, Math.Min(3, minDimension * 0.008));
            borderPadding = Math.Max(2, Math.Min(8, minDimension * 0.015));
        }
        else
        {
            countdownSize = Math.Max(16, Math.Min(60, minDimension * 0.25));
            borderMargin = Math.Max(2, Math.Min(5, minDimension * 0.012));
            borderPadding = Math.Max(3, Math.Min(12, minDimension * 0.025));
        }
        
        nextTimeSize = Math.Max(8, Math.Min(14, minDimension * 0.04));
        labelSize = Math.Max(9, Math.Min(16, minDimension * 0.045));
    }

    // Oblicz całkowitą wysokość zawartości
    double totalContentHeight = 0;
    
    if (_labelsVisible)
        totalContentHeight += labelSize * 1.5; // Label + margin
    
    totalContentHeight += countdownSize * 1.2; // Countdown
    
    if (_nextTimeVisible)
        totalContentHeight += nextTimeSize * 1.8; // NextTime + margin
    
    totalContentHeight += borderPadding * 2; // Padding góra + dół

    // Jeśli zawartość nie mieści się, zmniejsz czcionki proporcjonalnie
    double availableSpace = isHorizontal ? availableHeight : (availableHeight / 2);
    
    if (totalContentHeight > availableSpace * 0.95) // 95% dostępnej przestrzeni
    {
        double scaleFactor = (availableSpace * 0.95) / totalContentHeight;
        countdownSize *= scaleFactor;
        nextTimeSize *= scaleFactor;
        labelSize *= scaleFactor;
        borderPadding *= scaleFactor;
        borderMargin *= scaleFactor;
        
        Console.WriteLine($"⚠️ Scaling down: factor={scaleFactor:F2}, content={totalContentHeight:F0}, available={availableSpace:F0}");
    }

    // Zastosuj rozmiary czcionek
    Countdown10mText.FontSize = countdownSize;
    NextTime10mText.FontSize = nextTimeSize;
    Label10m.FontSize = labelSize;

    Countdown2hText.FontSize = countdownSize;
    NextTime2hText.FontSize = nextTimeSize;
    Label2h.FontSize = labelSize;

    // Zastosuj marginesy i padding
    var margin = new Thickness(borderMargin);
    var padding = new Thickness(borderPadding);

    Timer10mBorder.Margin = margin;
    Timer10mBorder.Padding = padding;
    Timer2hBorder.Margin = margin;
    Timer2hBorder.Padding = padding;

    // Dostosuj marginesy między elementami
    double innerMargin = Math.Max(2, labelSize * 0.3);
    
    Label10m.Margin = new Thickness(0, 0, 0, _labelsVisible ? innerMargin : 0);
    Label2h.Margin = new Thickness(0, 0, 0, _labelsVisible ? innerMargin : 0);
    
    NextTime10mText.Margin = new Thickness(0, _nextTimeVisible ? innerMargin : 0, 0, 0);
    NextTime2hText.Margin = new Thickness(0, _nextTimeVisible ? innerMargin : 0, 0, 0);
    
    Countdown10mText.Margin = new Thickness(Math.Max(1, borderPadding * 0.2));
    Countdown2hText.Margin = new Thickness(Math.Max(1, borderPadding * 0.2));

    Console.WriteLine($"📐 Fonts: countdown={countdownSize:F1}, label={labelSize:F1}, margin={borderMargin:F1}, padding={borderPadding:F1}");
}
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.Cleanup();
            _resizeDebounceTimer?.Stop();
        }
    }
}