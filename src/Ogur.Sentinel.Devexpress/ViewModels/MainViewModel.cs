using System;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly DesktopSettings _settings;
        
        private bool _isPinned;
        private string _userInfoText;
        private Visibility _userInfoVisibility = Visibility.Collapsed;
        private Visibility _logoutButtonVisibility = Visibility.Collapsed;
        private Visibility _settingsButtonVisibility = Visibility.Collapsed;

        public MainViewModel(ApiClient apiClient, DesktopSettings settings)
        {
            _apiClient = apiClient;
            _settings = settings;
            
            _isPinned = _settings.AlwaysOnTop;
            PinButtonContent = _isPinned ? "📌" : "📍";
            
            // Commands
            PinCommand = new DelegateCommand(OnPin);
            SettingsCommand = new DelegateCommand(OnSettings);
            MinimizeCommand = new DelegateCommand(OnMinimize);
            CloseCommand = new DelegateCommand(OnClose);
            LogoutCommand = new DelegateCommand(OnLogout);
        }

        #region Properties

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetProperty(ref _isPinned, value, nameof(IsPinned)))
                {
                    PinButtonContent = value ? "📌" : "📍";
                    _settings.AlwaysOnTop = value;
                    _settings.Save();
                }
            }
        }

        private string _pinButtonContent;
        public string PinButtonContent
        {
            get => _pinButtonContent;
            set => SetProperty(ref _pinButtonContent, value, nameof(PinButtonContent));
        }

        public string UserInfoText
        {
            get => _userInfoText;
            set => SetProperty(ref _userInfoText, value, nameof(UserInfoText));
        }

        public Visibility UserInfoVisibility
        {
            get => _userInfoVisibility;
            set => SetProperty(ref _userInfoVisibility, value, nameof(UserInfoVisibility));
        }

        public Visibility LogoutButtonVisibility
        {
            get => _logoutButtonVisibility;
            set => SetProperty(ref _logoutButtonVisibility, value, nameof(LogoutButtonVisibility));
        }

        public Visibility SettingsButtonVisibility
        {
            get => _settingsButtonVisibility;
            set => SetProperty(ref _settingsButtonVisibility, value, nameof(SettingsButtonVisibility));
        }

        #endregion

        #region Commands

        public ICommand PinCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand MinimizeCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand LogoutCommand { get; }

        private void OnPin()
        {
            IsPinned = !IsPinned;
        }

        private void OnSettings()
        {
            // Wysłanie komunikatu do MainWindow
            Messenger.Default.Send(new NavigateMessage { Target = "Settings" });
        }

        private void OnMinimize()
        {
            Messenger.Default.Send(new WindowStateMessage { State = WindowState.Minimized });
        }

        private void OnClose()
        {
            Application.Current.Shutdown();
        }

        private async void OnLogout()
        {
            await _apiClient.LogoutAsync();
            Messenger.Default.Send(new NavigateMessage { Target = "Login", TryAutoLogin = false });
        }

        #endregion

        #region Methods

        public void UpdateUserInfo()
        {
            if (_apiClient.IsAuthenticated)
            {
                UserInfoText = $"👤 {_apiClient.CurrentUsername} ({_apiClient.CurrentRole})";
                UserInfoVisibility = Visibility.Visible;
                LogoutButtonVisibility = Visibility.Visible;
                SettingsButtonVisibility = Visibility.Visible;
            }
            else
            {
                UserInfoVisibility = Visibility.Collapsed;
                LogoutButtonVisibility = Visibility.Collapsed;
                SettingsButtonVisibility = Visibility.Collapsed;
            }
        }

        public void HideUserInfo()
        {
            UserInfoVisibility = Visibility.Collapsed;
            LogoutButtonVisibility = Visibility.Collapsed;
            SettingsButtonVisibility = Visibility.Collapsed;
        }

        #endregion
    }

    // Messages dla komunikacji między ViewModels
    public class NavigateMessage
    {
        public string Target { get; set; }
        public bool TryAutoLogin { get; set; } = true;
    }

    public class WindowStateMessage
    {
        public WindowState State { get; set; }
    }
}