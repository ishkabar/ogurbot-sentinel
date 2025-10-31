using System;
using System.Windows;
using System.Windows.Input;
using DevExpress.Xpf.Core;
using DevExpress.Mvvm;
using Ogur.Sentinel.Devexpress.ViewModels;
using Ogur.Sentinel.Devexpress.Views;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress
{
    public partial class MainWindow : Window
    {
        private const string API_BASE_URL = "https://respy.ogur.dev";
        private static readonly DateTime LICENSE_EXPIRATION = new DateTime(2025, 11, 8);

        private readonly ApiClient _apiClient;
        private readonly DesktopSettings _settings;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Check license
            if (DateTime.Now > LICENSE_EXPIRATION)
            {
                var licenseWindow = new LicenseExpiredWindow(LICENSE_EXPIRATION);
                licenseWindow.ShowDialog();
                Application.Current.Shutdown();
                return;
            }

            _settings = DesktopSettings.Load();
            _apiClient = new ApiClient(API_BASE_URL);
            _viewModel = new MainViewModel(_apiClient, _settings);

            DataContext = _viewModel;

            // Set window properties
            Topmost = _settings.AlwaysOnTop;

            if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
            {
                Width = _settings.WindowWidth;
                Height = _settings.WindowHeight;
            }

            // Register for messages
            Messenger.Default.Register<NavigateMessage>(this, OnNavigateMessage);
            Messenger.Default.Register<WindowStateMessage>(this, OnWindowStateMessage);

            // Navigate to login
            NavigateToLogin(true);
        }

        #region Navigation

        private void OnNavigateMessage(NavigateMessage message)
        {
            switch (message.Target)
            {
                case "Login":
                    NavigateToLogin(message.TryAutoLogin);
                    break;
                case "Timers":
                    NavigateToTimers();
                    break;
                case "Settings":
                    NavigateToSettings();
                    break;
            }
        }

        public void NavigateToLogin(bool tryAutoLogin)
        {
            _viewModel.HideUserInfo();
            MinWidth = 300;
            MinHeight = 500;
            ContentFrame.Navigate(new LoginView(_apiClient, this,_settings, tryAutoLogin));
        }

        public void NavigateToTimers()
        {
            _viewModel.UpdateUserInfo();
            MinWidth = 130;
            MinHeight = 100;
            ContentFrame.Navigate(new TimerView(_apiClient, _settings));
        }

        public void NavigateToSettings()
        {
            _viewModel.UpdateUserInfo();
            MinWidth = 300;
            MinHeight = 400;
            ContentFrame.Navigate(new SettingsView(_apiClient, _settings));
        }

        #endregion

        #region Window Events

        private void OnWindowStateMessage(WindowStateMessage message)
        {
            WindowState = message.State;
        }

        protected override void OnClosed(EventArgs e)
        {
            _settings.WindowWidth = (int)Width;
            _settings.WindowHeight = (int)Height;
            _settings.Save();

            Messenger.Default.Unregister(this);
            base.OnClosed(e);
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        #endregion

        #region Commands (binding to ViewModel)

        // Commands są już w ViewModel i bindowane przez XAML

        #endregion
    }
}