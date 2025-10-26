using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ogur.Sentinel.Desktop.Config;
using Ogur.Sentinel.Desktop.Services;
using Ogur.Sentinel.Desktop.Views;

namespace Ogur.Sentinel.Desktop;

public partial class MainWindow : Window
{
    //private const string API_BASE_URL = "http://localhost:5205";
    private const string API_BASE_URL = "https://respy.ogur.dev";
    private static readonly DateTime LICENSE_EXPIRATION = new DateTime(2025, 11, 8);


    private readonly ApiClient _apiClient;
    private readonly DesktopSettings _settings;
    private bool _isPinned = true;
    private bool _isOnSettingsPage = false;

    public MainWindow()
    {
        InitializeComponent();
        
        // ✅ Check license before doing anything
        if (DateTime.Now > LICENSE_EXPIRATION)
        {
            // ✅ Show custom license expired window
            var licenseWindow = new LicenseExpiredWindow(LICENSE_EXPIRATION);
            licenseWindow.ShowDialog();
            Application.Current.Shutdown();
            return;
        }
        
        _settings = DesktopSettings.Load();
        _apiClient = new ApiClient(API_BASE_URL);  // ✅ Użyj hardcoded URL
        
        Topmost = _settings.AlwaysOnTop;
        _isPinned = _settings.AlwaysOnTop;
        PinButton.Content = _isPinned ? "📌" : "📍";
        
        if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
        {
            Width = _settings.WindowWidth;
            Height = _settings.WindowHeight;
        }
        
        ContentFrame.Navigate(new LoginView(_apiClient, this));
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.WindowWidth = (int)Width;
        _settings.WindowHeight = (int)Height;
        _settings.Save();

        base.OnClosed(e);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // ✅ Ukryj header gdy wysokość < 200px (wcześniej było 150)
        if (e.NewSize.Height < 200)
        {
            HeaderGrid.Visibility = Visibility.Collapsed;
            HeaderGrid.Height = 0;
        }
        else
        {
            HeaderGrid.Visibility = Visibility.Visible;
            HeaderGrid.Height = double.NaN;
        }

        // Opcjonalnie: ukryj tekst w headerze gdy szerokość < 200px
        if (e.NewSize.Width < 200)
        {
            UserInfoText.Visibility = Visibility.Collapsed;
        }
        else if (_apiClient.IsAuthenticated)
        {
            UserInfoText.Visibility = Visibility.Visible;
        }
    }

    public void NavigateToTimers()
    {
        ContentFrame.Navigate(new TimerView(_apiClient, this, _settings));
        UpdateUserInfo();
        UpdateSettingsButton();
        _isOnSettingsPage = false;
        UpdateSettingsButtonHighlight();

        MinWidth = 130;
        MinHeight = 100;
    }

    public void NavigateToSettings()
    {
        ContentFrame.Navigate(new SettingsView(_apiClient, this, _settings));
        UpdateUserInfo();
        UpdateSettingsButton();
        _isOnSettingsPage = true;
        UpdateSettingsButtonHighlight();

        // ✅ Przywróć większe minimum dla Settings
        MinWidth = 300;
        MinHeight = 400;
    }

    public void NavigateToLogin()
    {
        _apiClient.Logout();
        UserInfoText.Visibility = Visibility.Collapsed;
        SettingsButton.Visibility = Visibility.Collapsed;
        _isOnSettingsPage = false;
        ContentFrame.Navigate(new LoginView(_apiClient, this));

        // ✅ Login też potrzebuje więcej miejsca
        MinWidth = 300;
        MinHeight = 400;
    }

    private void UpdateUserInfo()
    {
        if (_apiClient.IsAuthenticated && Width >= 200)
        {
            UserInfoText.Text = $"👤 {_apiClient.CurrentUsername} ({_apiClient.CurrentRole})";
            UserInfoText.Visibility = Visibility.Visible;
        }
        else
        {
            UserInfoText.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSettingsButton()
    {
        if (_apiClient.CurrentRole == "Timer")
        {
            SettingsButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SettingsButton.Visibility = Visibility.Visible;
        }
    }

    private void UpdateSettingsButtonHighlight()
    {
        if (_isOnSettingsPage)
        {
            SettingsButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066cc"));
        }
        else
        {
            SettingsButton.Background = Brushes.Transparent;
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        PinButton.Content = _isPinned ? "📌" : "📍";

        _settings.AlwaysOnTop = _isPinned;
        _settings.Save();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_isOnSettingsPage)
        {
            NavigateToTimers();
        }
        else
        {
            NavigateToSettings();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}