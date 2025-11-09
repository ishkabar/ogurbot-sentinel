using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media; // ✅ Zostaw tylko raz
using System.Windows.Media.Animation; // ✅ Zostaw tylko raz
using System.Windows.Navigation;
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
        private static readonly DateTime LICENSE_EXPIRATION = new DateTime(2025, 11, 22);

        private readonly ApiClient _apiClient;
        private readonly DesktopSettings _settings;
        private readonly MainViewModel _viewModel;

        private TimerView? _cachedTimerView;
        private SettingsView? _cachedSettingsView;


        public MainWindow()
        {
            InitializeComponent();

            Console.WriteLine("🎬 [MainWindow] Starting initialization");

            // Check license
            if (DateTime.Now > LICENSE_EXPIRATION)
            {
                var licenseWindow = new LicenseExpiredWindow(LICENSE_EXPIRATION);
                licenseWindow.ShowDialog();
                Application.Current.Shutdown();
                return;
            }

            ContentFrame.Navigated += ContentFrame_Navigated;
            this.SizeChanged += MainWindow_SizeChanged_ForHeader;

            _settings = DesktopSettings.Load();
            Console.WriteLine(
                $"⚙️ [MainWindow] Settings loaded: ApiUrl={_settings.ApiUrl}, SyncInterval={_settings.SyncIntervalSeconds}");

            _apiClient = new ApiClient(_settings.ApiUrl);
            Console.WriteLine($"🌐 [MainWindow] ApiClient created with base URL: {_settings.ApiUrl}");

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
            Console.WriteLine("🚀 [MainWindow] Navigating to login");
            NavigateToLogin(true);
        }

        #region Navigation

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is LoginView)
            {
                this.ResizeMode = ResizeMode.NoResize;
                this.Width = 330;
                this.Height = 580;
                MinWidth = 330;
                MinHeight = 580;
                ShowHeaderAnimated();
            }
            else if (e.Content is TimerView)
            {
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                MinWidth = 130;
                MinHeight = 50;

                UpdateHeaderVisibility();
            }
            else if (e.Content is SettingsView)
            {
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                MinWidth = 300;
                MinHeight = 400;
                ShowHeaderAnimated();
            }
            else
            {
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                ShowHeaderAnimated();
            }
        }

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
            Console.WriteLine("🔄 [MainWindow] NavigateToLogin called");
            _viewModel.HideUserInfo();

            // Cleanup old timers
            if (_cachedTimerView != null)
            {
                Console.WriteLine("🧹 [MainWindow] Cleaning up TimerView");
                (_cachedTimerView.DataContext as TimerViewModel)?.Cleanup();
                _cachedTimerView = null;
            }

            if (_cachedSettingsView != null)
            {
                _cachedSettingsView = null;
            }

            ContentFrame.Navigate(new LoginView(_apiClient, this, _settings, tryAutoLogin));
        }

        public void NavigateToTimers()
        {
            Console.WriteLine("🔄 [MainWindow] NavigateToTimers called");
            _viewModel.UpdateUserInfo();

            if (_cachedTimerView == null)
            {
                Console.WriteLine("🆕 [MainWindow] Creating NEW TimerView");
                _cachedTimerView = new TimerView(_apiClient, _settings);
            }
            else
            {
                Console.WriteLine("♻️ [MainWindow] Reusing cached TimerView");
            }

            ContentFrame.Navigate(_cachedTimerView);
        }

        public void NavigateToSettings()
        {
            Console.WriteLine("🔄 [MainWindow] NavigateToSettings called");
            _viewModel.UpdateUserInfo();

            if (_cachedSettingsView == null)
            {
                Console.WriteLine("🆕 [MainWindow] Creating NEW SettingsView");
                _cachedSettingsView = new SettingsView(_apiClient, _settings);
            }
            else
            {
                Console.WriteLine("♻️ [MainWindow] Reusing cached SettingsView");
            }

            ContentFrame.Navigate(_cachedSettingsView);
        }

        #endregion

        #region Header Animation

        private bool _isHeaderVisible = true;
        private bool _isAnimating = false;

        private void MainWindow_SizeChanged_ForHeader(object sender, SizeChangedEventArgs e)
        {
            UpdateHeaderVisibility();
        }
        
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Ignore if DragMove fails (can happen during rapid clicks)
                }
            }
        }

        private void UpdateHeaderVisibility()
        {
            if (ContentFrame.Content is TimerView)
            {
                bool shouldBeVisible = this.ActualHeight >= 150;

                // ✅ Animuj tylko jeśli stan się zmienia i nie animujemy już
                if (shouldBeVisible != _isHeaderVisible && !_isAnimating)
                {
                    if (shouldBeVisible)
                    {
                        ShowHeaderAnimated();
                    }
                    else
                    {
                        HideHeaderAnimated();
                    }
                }
            }
        }

private void HideHeaderAnimated()
{
    if (_isAnimating || !_isHeaderVisible) return;

    _isAnimating = true;
    _isHeaderVisible = false;

    var transform = HeaderGrid.RenderTransform as TranslateTransform;
    if (transform == null) 
    {
        _isAnimating = false;
        return;
    }

    var slideOut = new DoubleAnimation
    {
        From = transform.Y,
        To = -50,
        Duration = TimeSpan.FromMilliseconds(300), // ✅ Zwiększono z 200 na 300
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } // ✅ Zmieniono na EaseInOut
    };

    var fadeOut = new DoubleAnimation
    {
        From = HeaderGrid.Opacity,
        To = 0,
        Duration = TimeSpan.FromMilliseconds(300), // ✅ Zwiększono z 200 na 300
    };

    slideOut.Completed += (s, e) =>
    {
        HeaderGrid.Visibility = Visibility.Collapsed;
        _isAnimating = false;
    };

    transform.BeginAnimation(TranslateTransform.YProperty, slideOut);
    HeaderGrid.BeginAnimation(OpacityProperty, fadeOut);
}

private void ShowHeaderAnimated()
{
    if (_isAnimating || _isHeaderVisible) return;

    _isAnimating = true;
    _isHeaderVisible = true;

    var transform = HeaderGrid.RenderTransform as TranslateTransform;
    if (transform == null)
    {
        _isAnimating = false;
        return;
    }

    HeaderGrid.Visibility = Visibility.Visible;

    var slideIn = new DoubleAnimation
    {
        From = transform.Y,
        To = 0,
        Duration = TimeSpan.FromMilliseconds(350), // ✅ Zwiększono z 250 na 350
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };

    var fadeIn = new DoubleAnimation
    {
        From = HeaderGrid.Opacity,
        To = 1,
        Duration = TimeSpan.FromMilliseconds(350), // ✅ Zwiększono z 250 na 350
    };

    slideIn.Completed += (s, e) =>
    {
        _isAnimating = false;
    };

    transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
    HeaderGrid.BeginAnimation(OpacityProperty, fadeIn);
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
    }
}