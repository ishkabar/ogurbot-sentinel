using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ogur.Sentinel.Devexpress.ViewModels;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.Views
{
    public partial class TimerView : Page
    {
        private readonly TimerViewModel _viewModel;
        private DispatcherTimer _resizeDebounceTimer;

        public TimerView(ApiClient apiClient, DesktopSettings settings)
        {
            InitializeComponent();

            _viewModel = new TimerViewModel(apiClient, settings);
            DataContext = _viewModel;
            
            _resizeDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            
            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
                Console.WriteLine($"📏 Final size: Width={ActualWidth:F0}, Height={ActualHeight:F0}");
            };
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdaptLayout(e.NewSize.Width, e.NewSize.Height);
            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer?.Start();
            
            // Czyścimy obecny layout
            TimersContainer.RowDefinitions.Clear();
            TimersContainer.ColumnDefinitions.Clear();
    
            var width = ActualWidth;
            var height = ActualHeight;
    
            // Decydujemy o layoucie na podstawie rozmiaru okna
            bool isHorizontal = width > 600; // Próg dla poziomego układu
    
            if (isHorizontal)
            {
                // Layout poziomy (2 kolumny, 1 wiersz)
                TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        
                Grid.SetRow(Timer10mBorder, 0);
                Grid.SetColumn(Timer10mBorder, 0);
        
                Grid.SetRow(Timer2hBorder, 0);
                Grid.SetColumn(Timer2hBorder, 1);
            }
            else
            {
                // Layout pionowy (1 kolumna, 2 wiersze)
                TimersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                TimersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        
                Grid.SetRow(Timer10mBorder, 0);
                Grid.SetColumn(Timer10mBorder, 0);
        
                Grid.SetRow(Timer2hBorder, 1);
                Grid.SetColumn(Timer2hBorder, 0);
            }
        }

        private void AdaptLayout(double width, double height)
        {
            TimersContainer.RowDefinitions.Clear();
            TimersContainer.ColumnDefinitions.Clear();

            // ... CAŁA RESZTA TWOJEGO KODU BEZ ZMIAN ...
            // (skopiuj z poprzedniej wersji)
        }

        private void AdjustFontSizes(double availableWidth, double availableHeight, bool isHorizontal)
        {
            // ... CAŁA RESZTA TWOJEGO KODU BEZ ZMIAN ...
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.Cleanup();
            _resizeDebounceTimer?.Stop();
        }
    }
}