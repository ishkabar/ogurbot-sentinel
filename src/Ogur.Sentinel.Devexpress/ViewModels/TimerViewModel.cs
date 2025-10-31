using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using DevExpress.Mvvm;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.ViewModels
{
    public class TimerViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly DesktopSettings _settings;
        private readonly DispatcherTimer _countdownTimer;
        private readonly DispatcherTimer _syncTimer;

        private DateTime? _actualNext10m;
        private DateTime? _actualNext2h;

        // Properties
        private string _countdown10mText = "--:--:--";
        private string _countdown2hText = "--:--:--";
        private string _nextTime10mText = "Next: --:--:--";
        private string _nextTime2hText = "Next: --:--:--";
        private string _statusText = "Last synced: Never";
        private Brush _countdown10mForeground = Brushes.White;
        private Brush _countdown2hForeground = Brushes.White;
        private Brush _statusForeground = Brushes.Gray;

        public TimerViewModel(ApiClient apiClient, DesktopSettings settings)
        {
            _apiClient = apiClient;
            _settings = settings;

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += (s, e) => UpdateCountdowns();
            _countdownTimer.Start();

            _syncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(settings.SyncIntervalSeconds)
            };
            _syncTimer.Tick += async (s, e) => await SyncWithApi();
            _syncTimer.Start();

            // Initial sync
            _ = SyncWithApi();
        }

        #region Properties

        public string Countdown10mText
        {
            get => _countdown10mText;
            set => SetProperty(ref _countdown10mText, value, nameof(Countdown10mText));
        }

        public string Countdown2hText
        {
            get => _countdown2hText;
            set => SetProperty(ref _countdown2hText, value, nameof(Countdown2hText));
        }

        public string NextTime10mText
        {
            get => _nextTime10mText;
            set => SetProperty(ref _nextTime10mText, value, nameof(NextTime10mText));
        }

        public string NextTime2hText
        {
            get => _nextTime2hText;
            set => SetProperty(ref _nextTime2hText, value, nameof(NextTime2hText));
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value, nameof(StatusText));
        }

        public Brush Countdown10mForeground
        {
            get => _countdown10mForeground;
            set => SetProperty(ref _countdown10mForeground, value, nameof(Countdown10mForeground));
        }

        public Brush Countdown2hForeground
        {
            get => _countdown2hForeground;
            set => SetProperty(ref _countdown2hForeground, value, nameof(Countdown2hForeground));
        }

        public Brush StatusForeground
        {
            get => _statusForeground;
            set => SetProperty(ref _statusForeground, value, nameof(StatusForeground));
        }

        #endregion

        #region Methods

        private void UpdateCountdowns()
        {
            var now = DateTime.Now;

            // 10m timer
            if (_actualNext10m.HasValue)
            {
                var remaining = _actualNext10m.Value - now;

                if (remaining.TotalSeconds > 10)
                {
                    Countdown10mText = FormatTimeSpan(remaining);
                    Countdown10mForeground = GetColorForTimeRemaining(remaining);
                }
                else if (remaining.TotalSeconds > 0)
                {
                    Countdown10mText = FormatTimeSpan(remaining);
                    Countdown10mForeground = Brushes.Red;
                }
                else if (remaining.TotalSeconds > -10)
                {
                    Countdown10mText = "RESPAWN!";
                    Countdown10mForeground = Brushes.Red;
                }
                else
                {
                    Countdown10mText = "Syncing...";
                    Countdown10mForeground = Brushes.Orange;
                }
            }
            else
            {
                Countdown10mText = "--:--:--";
                Countdown10mForeground = Brushes.White;
            }

            // 2h timer
            if (_actualNext2h.HasValue)
            {
                var remaining = _actualNext2h.Value - now;

                if (remaining.TotalSeconds > 10)
                {
                    Countdown2hText = FormatTimeSpan(remaining);
                    Countdown2hForeground = GetColorForTimeRemaining(remaining);
                }
                else if (remaining.TotalSeconds > 0)
                {
                    Countdown2hText = FormatTimeSpan(remaining);
                    Countdown2hForeground = Brushes.Red;
                }
                else if (remaining.TotalSeconds > -10)
                {
                    Countdown2hText = "RESPAWN!";
                    Countdown2hForeground = Brushes.Red;
                }
                else
                {
                    Countdown2hText = "Syncing...";
                    Countdown2hForeground = Brushes.Orange;
                }
            }
            else
            {
                Countdown2hText = "--:--:--";
                Countdown2hForeground = Brushes.White;
            }
        }

        private async Task SyncWithApi()
        {
            try
            {
                var times = await _apiClient.GetNextRespawnAsync();

                if (times != null)
                {
                    _actualNext10m = times.Next10m.ToLocalTime();
                    _actualNext2h = times.Next2h.ToLocalTime();

                    var displayNext10m = _actualNext10m.Value.AddSeconds(-_settings.TimeOffsetSeconds);
                    var displayNext2h = _actualNext2h.Value.AddSeconds(-_settings.TimeOffsetSeconds);

                    NextTime10mText = $"Next: {displayNext10m:HH:mm:ss}";
                    NextTime2hText = $"Next: {displayNext2h:HH:mm:ss}";

                    StatusText = $"Synced: {DateTime.Now:HH:mm:ss}";
                    StatusForeground = Brushes.Green;

                    UpdateCountdowns();
                }
                else
                {
                    StatusText = "Sync failed";
                    StatusForeground = Brushes.Red;
                }
            }
            catch
            {
                StatusText = "Error";
                StatusForeground = Brushes.Red;
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            else
            {
                return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
        }

        private Brush GetColorForTimeRemaining(TimeSpan remaining)
        {
            if (remaining.TotalMinutes < _settings.WarningMinutesRed)
            {
                return Brushes.Red;
            }
            else if (remaining.TotalMinutes < _settings.WarningMinutesOrange)
            {
                return Brushes.Orange;
            }
            else
            {
                return Brushes.White;
            }
        }

        public void Cleanup()
        {
            _countdownTimer?.Stop();
            _syncTimer?.Stop();
        }

        #endregion
    }
}