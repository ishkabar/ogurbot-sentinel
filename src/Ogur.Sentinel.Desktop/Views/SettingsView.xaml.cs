using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ogur.Sentinel.Desktop.Config;
using Ogur.Sentinel.Desktop.Services;

namespace Ogur.Sentinel.Desktop.Views;

public partial class SettingsView : Page
{
    private readonly ApiClient _apiClient;
    private readonly MainWindow _mainWindow;
    private readonly DesktopSettings _settings;

    public SettingsView(ApiClient apiClient, MainWindow mainWindow, DesktopSettings settings)
    {
        InitializeComponent();
        
        _apiClient = apiClient;
        _mainWindow = mainWindow;
        _settings = settings;
        
        _mainWindow.MinWidth = 300;
        _mainWindow.MinHeight = 400;
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        SyncIntervalBox.Text = _settings.SyncIntervalSeconds.ToString();
        TimeOffsetBox.Text = _settings.TimeOffsetSeconds.ToString();
        WarningRedBox.Text = _settings.WarningMinutesRed.ToString();
        WarningOrangeBox.Text = _settings.WarningMinutesOrange.ToString();
        
        ShowStatus("Settings loaded", Colors.Green);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // ✅ Parse sync interval
            if (!int.TryParse(SyncIntervalBox.Text, out var syncInterval) || syncInterval < 5)
            {
                ShowStatus("Sync interval must be at least 5 seconds", Colors.Red);
                return;
            }
            
            // ✅ Parse time offset (może być ujemny!)
            if (!int.TryParse(TimeOffsetBox.Text, out var timeOffset))
            {
                ShowStatus("Time offset must be a valid number (can be negative)", Colors.Red);
                return;
            }
            
            // ✅ Parse warning times
            if (!int.TryParse(WarningRedBox.Text, out var warningRed) || warningRed < 1)
            {
                ShowStatus("Warning red must be at least 1 minute", Colors.Red);
                return;
            }
            
            if (!int.TryParse(WarningOrangeBox.Text, out var warningOrange) || warningOrange <= warningRed)
            {
                ShowStatus("Warning orange must be greater than warning red", Colors.Red);
                return;
            }
            
            // ✅ Zapisz ustawienia
            _settings.SyncIntervalSeconds = syncInterval;
            _settings.TimeOffsetSeconds = timeOffset;
            _settings.WarningMinutesRed = warningRed;
            _settings.WarningMinutesOrange = warningOrange;
            _settings.Save();
            
            ShowStatus("Settings saved! Please restart the app for changes to take effect. ✅", Colors.Green);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", Colors.Red);
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        // ✅ Przywróć domyślne wartości
        _settings.SyncIntervalSeconds = 30;
        _settings.TimeOffsetSeconds = 0;
        _settings.WarningMinutesRed = 5;
        _settings.WarningMinutesOrange = 10;
        _settings.Save();
        
        LoadSettings();
        ShowStatus("Settings reset to defaults", Colors.Green);
    }

    private void Timers_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.NavigateToTimers();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.NavigateToLogin(false);
    }

    private void ShowStatus(string message, Color color)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(color);
        StatusText.Visibility = Visibility.Visible;
        
        // Auto-hide after 5 seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += (s, e) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }
}