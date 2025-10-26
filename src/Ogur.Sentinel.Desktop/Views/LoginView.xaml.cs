using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ogur.Sentinel.Desktop.Services;

namespace Ogur.Sentinel.Desktop.Views;

public partial class LoginView : Page
{
    private readonly ApiClient _apiClient;
    private readonly MainWindow _mainWindow;

    public LoginView(ApiClient apiClient, MainWindow mainWindow)
    {
        InitializeComponent();
        
        _apiClient = apiClient;
        _mainWindow = mainWindow;
        
        _mainWindow.MinWidth = 300;
        _mainWindow.MinHeight = 400;
        
        // ✅ Focus na username przy starcie
        UsernameBox.Focus();
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
            _mainWindow.NavigateToTimers();
        }
        else
        {
            ErrorText.Text = error ?? "Login failed";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
