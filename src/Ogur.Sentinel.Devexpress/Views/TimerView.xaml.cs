using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ogur.Sentinel.Devexpress.ViewModels;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;
using Ogur.Sentinel.Devexpress.Views.Scaling;

namespace Ogur.Sentinel.Devexpress.Views
{
    public partial class TimerView : Page
    {
        private readonly TimerViewModel _viewModel;
        private TimerLayoutScaler _layoutScaler;
        private ScalingConfig _scalingConfig;
        private DispatcherTimer _resizeDebounceTimer;
        private DispatcherTimer _configReloadTimer;
        private FileSystemWatcher _configFileWatcher;
        private DateTime _lastConfigReload = DateTime.MinValue;
        private string _lastConfigHash = string.Empty;

        public TimerView(ApiClient apiClient, DesktopSettings settings)
        {
            InitializeComponent();

            _viewModel = new TimerViewModel(apiClient, settings);
            DataContext = _viewModel;

            // Załaduj konfigurację skalowania
            _scalingConfig = ScalingConfig.Load();
            
            if (App.DebugMode)
            {
                _lastConfigHash = GetConfigFileHash();
                Console.WriteLine($"🔑 [Init] Initial config hash: {_lastConfigHash}");

                // Timer do sprawdzania configu co 3 sekundy (tylko w debug mode)
                _configReloadTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _configReloadTimer.Tick += (s, e) => CheckAndReloadConfig();
                _configReloadTimer.Start();

                // FileSystemWatcher do natychmiastowego wykrywania zmian (tylko w debug mode)
                SetupConfigFileWatcher();
            }

            // ✅ Inicjalizuj scaler z referencjami do wszystkich elementów UI (w tym StatusText)
            _layoutScaler = new TimerLayoutScaler(
                TimersContainer,
                MainGrid,
                Timer10mBorder,
                Timer2hBorder,
                Countdown10mText,
                Countdown2hText,
                NextTime10mText,
                NextTime2hText,
                Label10m,
                Label2h,
                Label10mScale,
                Label2hScale,
                NextTime10mScale,
                NextTime2hScale,
                HeaderScale,
                HeaderText,
                StatusText,          // ✅ Dodane
                StatusTextScale,     // ✅ Dodane
                _scalingConfig
            );

            _resizeDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };

            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
            };

            // Zainicjalizuj layout po załadowaniu
            this.Loaded += (s, e) =>
            {
                _layoutScaler.InitializeLayout();

                if (ActualWidth > 0 && ActualHeight > 0)
                {
                    _layoutScaler.AdaptLayout(ActualWidth, ActualHeight, DispatcherInvokeHelper);
                }
            };
        }

        private void SetupConfigFileWatcher()
        {
            if (!App.DebugMode) return;

            try
            {
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "OgurSentinel"
                );

                if (!Directory.Exists(appDataFolder))
                {
                    Directory.CreateDirectory(appDataFolder);
                }

                _configFileWatcher = new FileSystemWatcher(appDataFolder)
                {
                    Filter = "scaling-config.json",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                _configFileWatcher.Changed += (s, e) => 
                {
                    Console.WriteLine("📂 [ConfigWatcher] File changed detected!");
                    CheckAndReloadConfig();
                };

                _configFileWatcher.Created += (s, e) => 
                {
                    Console.WriteLine("📂 [ConfigWatcher] File created detected!");
                    CheckAndReloadConfig();
                };

                Console.WriteLine($"👁️ [ConfigWatcher] Watching: {appDataFolder}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ConfigWatcher] Setup failed: {ex.Message}");
            }
        }

        private void CheckAndReloadConfig()
        {
            if (!App.DebugMode) return;

            try
            {
                // Debounce - nie ładuj częściej niż co 1 sekundę
                if ((DateTime.Now - _lastConfigReload).TotalSeconds < 1)
                    return;

                string currentHash = GetConfigFileHash();
                
                if (string.IsNullOrEmpty(currentHash))
                {
                    Console.WriteLine("⚠️ [ConfigReload] Cannot read config file hash");
                    return;
                }

                if (currentHash != _lastConfigHash)
                {
                    Console.WriteLine($"🔄 [ConfigReload] Hash changed!");
                    Console.WriteLine($"   Old: {_lastConfigHash}");
                    Console.WriteLine($"   New: {currentHash}");
                    
                    var newConfig = ScalingConfig.Load();
                    _scalingConfig = newConfig;
                    _lastConfigHash = currentHash;
                    _lastConfigReload = DateTime.Now;

                    // Utwórz nowy scaler z nowym configiem
                    Dispatcher.Invoke(() =>
                    {
                        _layoutScaler = new TimerLayoutScaler(
                            TimersContainer,
                            MainGrid,
                            Timer10mBorder,
                            Timer2hBorder,
                            Countdown10mText,
                            Countdown2hText,
                            NextTime10mText,
                            NextTime2hText,
                            Label10m,
                            Label2h,
                            Label10mScale,
                            Label2hScale,
                            NextTime10mScale,
                            NextTime2hScale,
                            HeaderScale,
                            HeaderText,
                            StatusText,
                            StatusTextScale,
                            _scalingConfig
                        );

                        // ✅ Zainicjalizuj stan wszystkich elementów
                        _layoutScaler.InitializeLayout();

                        if (ActualWidth > 0 && ActualHeight > 0)
                        {
                            _layoutScaler.AdaptLayout(ActualWidth, ActualHeight, DispatcherInvokeHelper);
                        }
                    });

                    Console.WriteLine("✅ [ConfigReload] Config reloaded and applied!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ConfigReload] Failed: {ex.Message}");
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _layoutScaler.AdaptLayout(e.NewSize.Width, e.NewSize.Height, DispatcherInvokeHelper);

            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer?.Start();
        }

        private void DispatcherInvokeHelper(Action action)
        {
            Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        private void Page_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Window.GetWindow(this)?.DragMove();
            }
        }

        private string GetConfigFileHash()
        {
            if (!App.DebugMode) return string.Empty;

            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OgurSentinel",
                    "scaling-config.json"
                );

                if (!File.Exists(configPath))
                    return string.Empty;

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using (var sha256 = SHA256.Create())
                        {
                            using (var stream = File.OpenRead(configPath))
                            {
                                byte[] hash = sha256.ComputeHash(stream);
                                return BitConverter.ToString(hash).Replace("-", "");
                            }
                        }
                    }
                    catch (IOException) when (i < 2)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [GetConfigFileHash] Failed: {ex.Message}");
                return string.Empty;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.Cleanup();
            _resizeDebounceTimer?.Stop();
            _configReloadTimer?.Stop();
            _configFileWatcher?.Dispose();
        }
    }
}