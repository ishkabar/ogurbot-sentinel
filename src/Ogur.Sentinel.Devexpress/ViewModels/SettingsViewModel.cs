using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Reflection;
using DevExpress.Mvvm;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;
using System.Linq;

namespace Ogur.Sentinel.Devexpress.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly DesktopSettings _settings;
        
        public string AppVersion { get; }
        public string BuildInfo { get; }

        private int _syncInterval;
        private int _timeOffset;
        private int _warningRed;
        private int _warningOrange;
        private string _statusText;
        private Visibility _statusVisibility = Visibility.Collapsed;
        private Brush _statusForeground = Brushes.Green;

        public SettingsViewModel(ApiClient apiClient, DesktopSettings settings)
        {
            _apiClient = apiClient;
            _settings = settings;
            
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            AppVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}";
        
            // Pobierz BuildTime z metadata
            var buildTimeAttr = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildTime");
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        
            BuildInfo = buildTimeAttr != null 
                ? $"Build: {buildTimeAttr.Value} | {informationalVersion}" 
                : informationalVersion ?? "Development Build";

            // Commands
            SaveCommand = new DelegateCommand(OnSave);
            ResetCommand = new DelegateCommand(OnReset);
            BackToTimersCommand = new DelegateCommand(OnBackToTimers);
            LogoutCommand = new DelegateCommand(OnLogout);

            // Load settings
            LoadSettings();
        }

        #region Properties

        public int SyncInterval
        {
            get => _syncInterval;
            set => SetProperty(ref _syncInterval, value, nameof(SyncInterval));
        }

        public int TimeOffset
        {
            get => _timeOffset;
            set => SetProperty(ref _timeOffset, value, nameof(TimeOffset));
        }

        public int WarningRed
        {
            get => _warningRed;
            set => SetProperty(ref _warningRed, value, nameof(WarningRed));
        }

        public int WarningOrange
        {
            get => _warningOrange;
            set => SetProperty(ref _warningOrange, value, nameof(WarningOrange));
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value, nameof(StatusText));
        }

        public Visibility StatusVisibility
        {
            get => _statusVisibility;
            set => SetProperty(ref _statusVisibility, value, nameof(StatusVisibility));
        }

        public Brush StatusForeground
        {
            get => _statusForeground;
            set => SetProperty(ref _statusForeground, value, nameof(StatusForeground));
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand BackToTimersCommand { get; }
        public ICommand LogoutCommand { get; }

        private void OnSave()
        {
            // Validate
            if (SyncInterval < 5)
            {
                ShowStatus("Sync interval must be at least 5 seconds", Brushes.Red);
                return;
            }

            if (WarningRed < 1)
            {
                ShowStatus("Warning red must be at least 1 minute", Brushes.Red);
                return;
            }

            if (WarningOrange <= WarningRed)
            {
                ShowStatus("Warning orange must be greater than warning red", Brushes.Red);
                return;
            }

            // Save
            _settings.SyncIntervalSeconds = SyncInterval;
            _settings.TimeOffsetSeconds = TimeOffset;
            _settings.WarningMinutesRed = WarningRed;
            _settings.WarningMinutesOrange = WarningOrange;
            _settings.Save();

            ShowStatus("Settings saved! Please restart the app for changes to take effect. ✅", Brushes.Green);
        }

        private void OnReset()
        {
            _settings.SyncIntervalSeconds = 30;
            _settings.TimeOffsetSeconds = 0;
            _settings.WarningMinutesRed = 5;
            _settings.WarningMinutesOrange = 10;
            _settings.Save();

            LoadSettings();
            ShowStatus("Settings reset to defaults", Brushes.Green);
        }

        private void OnBackToTimers()
        {
            Messenger.Default.Send(new NavigateMessage { Target = "Timers" });
        }

        private async void OnLogout()
        {
            await _apiClient.LogoutAsync();
            Messenger.Default.Send(new NavigateMessage { Target = "Login", TryAutoLogin = false });
        }

        #endregion

        #region Methods

        private void LoadSettings()
        {
            SyncInterval = _settings.SyncIntervalSeconds;
            TimeOffset = _settings.TimeOffsetSeconds;
            WarningRed = _settings.WarningMinutesRed;
            WarningOrange = _settings.WarningMinutesOrange;

            ShowStatus("Settings loaded", Brushes.Green);
        }

        private void ShowStatus(string message, Brush color)
        {
            StatusText = message;
            StatusForeground = color;
            StatusVisibility = Visibility.Visible;
        }

        #endregion
    }
}