using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevExpress.Mvvm;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly DesktopSettings _settings;
        private readonly bool _tryAutoLogin;

        private string _username;
        private string _password;
        private bool _rememberNothing = true;
        private bool _rememberUsername;
        private bool _rememberBoth;
        private string _errorText;
        private Visibility _errorVisibility = Visibility.Collapsed;
        private Brush _errorForeground = Brushes.Red;

        public LoginViewModel(ApiClient apiClient, DesktopSettings settings, bool tryAutoLogin = true)
        {
            _apiClient = apiClient;
            _settings = settings;
            _tryAutoLogin = tryAutoLogin;

            // Commands
            LoginCommand = new DelegateCommand(async () => await PerformLogin());

            // Load saved credentials
            LoadSavedCredentials();
        }

        #region Properties

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value, nameof(Username));
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value, nameof(Password));
        }

        public bool RememberNothing
        {
            get => _rememberNothing;
            set => SetProperty(ref _rememberNothing, value, nameof(RememberNothing));
        }

        public bool RememberUsername
        {
            get => _rememberUsername;
            set => SetProperty(ref _rememberUsername, value, nameof(RememberUsername));
        }

        public bool RememberBoth
        {
            get => _rememberBoth;
            set => SetProperty(ref _rememberBoth, value, nameof(RememberBoth));
        }

        public string ErrorText
        {
            get => _errorText;
            set => SetProperty(ref _errorText, value, nameof(ErrorText));
        }

        public Visibility ErrorVisibility
        {
            get => _errorVisibility;
            set => SetProperty(ref _errorVisibility, value, nameof(ErrorVisibility));
        }

        public Brush ErrorForeground
        {
            get => _errorForeground;
            set => SetProperty(ref _errorForeground, value, nameof(ErrorForeground));
        }

        #endregion

        #region Commands

        public ICommand LoginCommand { get; }

        #endregion

        #region Methods

        private void LoadSavedCredentials()
        {
            if (!string.IsNullOrEmpty(_settings.Username))
            {
                Username = _settings.Username;

                if (!string.IsNullOrEmpty(_settings.HashedPassword))
                {
                    RememberBoth = true;
                    
                    if (_tryAutoLogin)
                    {
                        _ = TryAutoLogin();
                    }
                }
                else
                {
                    RememberUsername = true;
                }
            }
            else
            {
                RememberNothing = true;
            }
        }

        private async Task TryAutoLogin()
        {
            await Task.Delay(100);

            var username = _settings.Username;
            var password = _settings.GetPassword();

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                ErrorText = "Auto-logging in...";
                ErrorForeground = Brushes.Green;
                ErrorVisibility = Visibility.Visible;

                var (success, role, error) = await _apiClient.LoginAsync(username, password);

                if (success)
                {
                    Messenger.Default.Send(new NavigateMessage { Target = "Timers" });
                }
                else
                {
                    ErrorText = "Auto-login failed. Please login manually.";
                    ErrorForeground = Brushes.Red;
                    ErrorVisibility = Visibility.Visible;
                }
            }
        }

        private async Task PerformLogin()
        {
            ErrorVisibility = Visibility.Collapsed;

            var username = Username?.Trim();
            var password = Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ErrorText = "Please enter username and password";
                ErrorForeground = Brushes.Red;
                ErrorVisibility = Visibility.Visible;
                return;
            }

            var (success, role, error) = await _apiClient.LoginAsync(username, password);

            if (success)
            {
                SaveCredentials(username, password);
                Messenger.Default.Send(new NavigateMessage { Target = "Timers" });
            }
            else
            {
                ErrorText = error ?? "Login failed";
                ErrorForeground = Brushes.Red;
                ErrorVisibility = Visibility.Visible;
            }
        }

        private void SaveCredentials(string username, string password)
        {
            if (RememberNothing)
            {
                _settings.Username = "";
                _settings.HashedPassword = "";
            }
            else if (RememberUsername)
            {
                _settings.Username = username;
                _settings.HashedPassword = "";
            }
            else if (RememberBoth)
            {
                _settings.Username = username;
                _settings.SetPassword(password);
            }

            _settings.Save();
        }

        #endregion
    }
}