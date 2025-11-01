using System;
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
        private readonly TimerLayoutScaler _layoutScaler;
        private DispatcherTimer _resizeDebounceTimer;

        public TimerView(ApiClient apiClient, DesktopSettings settings)
        {
            InitializeComponent();

            _viewModel = new TimerViewModel(apiClient, settings);
            DataContext = _viewModel;

            // Inicjalizuj scaler z referencjami do elementów UI
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
                HeaderText
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
                // Ustaw domyślny layout pionowy
                _layoutScaler.InitializeLayout();

                // Wywołaj adapt z aktualnymi wymiarami
                if (ActualWidth > 0 && ActualHeight > 0)
                {
                    _layoutScaler.AdaptLayout(ActualWidth, ActualHeight, DispatcherInvokeHelper);
                }
            };
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.Cleanup();
            _resizeDebounceTimer?.Stop();
        }
    }
}