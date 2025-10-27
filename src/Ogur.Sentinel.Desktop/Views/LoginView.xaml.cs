using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ogur.Sentinel.Desktop.Config;
using Ogur.Sentinel.Desktop.Services;

namespace Ogur.Sentinel.Desktop.Views;

public partial class LoginView : Page
{
    private readonly ApiClient _apiClient;
    private readonly MainWindow _mainWindow;
    private readonly DesktopSettings _settings;
    private readonly bool _tryAutoLogin;


    public LoginView(ApiClient apiClient, MainWindow mainWindow, DesktopSettings settings, bool tryAutoLogin = true)
    {
        InitializeComponent();
        
        _apiClient = apiClient;
        _mainWindow = mainWindow;
        _settings = settings;
                _tryAutoLogin = tryAutoLogin;

        
        _mainWindow.MinWidth = 300;
        _mainWindow.MinHeight = 500;
        
        // ✅ Załaduj zapisane dane
        LoadSavedCredentials();
        
        // ✅ Focus na username lub password w zależności od tego co jest zapisane
        if (string.IsNullOrEmpty(UsernameBox.Text))
            UsernameBox.Focus();
        else
            PasswordBox.Focus();
    }

    private void LoadSavedCredentials()
    {
        // Jeśli mamy zapisany username, załaduj go
        if (!string.IsNullOrEmpty(_settings.Username))
        {
            UsernameBox.Text = _settings.Username;
            
            // Jeśli mamy też hasło, załaduj je i ustaw odpowiedni radio
            if (!string.IsNullOrEmpty(_settings.HashedPassword))
            {
                RememberBothRadio.IsChecked = true;
                
                // ✅ Spróbuj auto-login TYLKO jeśli tryAutoLogin = true
                if (_tryAutoLogin)
                {
                    _ = TryAutoLogin();
                }
            }
            else
            {
                RememberUsernameRadio.IsChecked = true;
            }
        }
        else
        {
            RememberNothingRadio.IsChecked = true;
        }
    }

    private async Task TryAutoLogin()
    {
        // Zaczekaj chwilę żeby UI się załadował
        await Task.Delay(100);
        
        var username = _settings.Username;
        var password = _settings.GetPassword();
        
        // Jeśli mamy zapisane zarówno username jak i hasło, spróbuj zalogować
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            // Pokaż status "Auto-logging in..."
            ErrorText.Text = "Auto-logging in...";
            ErrorText.Foreground = System.Windows.Media.Brushes.Green;
            ErrorText.Visibility = Visibility.Visible;
            
            var (success, role, error) = await _apiClient.LoginAsync(username, password);

            if (success)
            {
                // Sukces - idź do timerów
                _mainWindow.NavigateToTimers();
            }
            else
            {
                // Niepowodzenie - pokaż błąd i pozwól userowi ręcznie się zalogować
                ErrorText.Text = "Auto-login failed. Please login manually.";
                ErrorText.Foreground = System.Windows.Media.Brushes.Red;
                ErrorText.Visibility = Visibility.Visible;
                
                // Focus na password żeby user mógł od razu wpisać
                PasswordBox.Focus();
            }
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await PerformLogin();
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await PerformLogin();
    }

    private async Task PerformLogin()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "Please enter username and password";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var (success, role, error) = await _apiClient.LoginAsync(username, password);

        if (success)
        {
            // ✅ Zapisz dane zgodnie z wyborem użytkownika
            SaveCredentials(username, password);
            
            _mainWindow.NavigateToTimers();
        }
        else
        {
            ErrorText.Text = error ?? "Login failed";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void SaveCredentials(string username, string password)
    {
        if (RememberNothingRadio.IsChecked == true)
        {
            // Wyczyść wszystko
            _settings.Username = "";
            _settings.HashedPassword = "";
        }
        else if (RememberUsernameRadio.IsChecked == true)
        {
            // Zapisz tylko username
            _settings.Username = username;
            _settings.HashedPassword = "";
        }
        else if (RememberBothRadio.IsChecked == true)
        {
            // Zapisz username i zahashowane hasło
            _settings.Username = username;
            _settings.SetPassword(password);
        }
        
        _settings.Save();
    }
}