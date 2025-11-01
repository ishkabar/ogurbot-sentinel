using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Ogur.Sentinel.Devexpress.Views.Scaling
{
    public class TimerLayoutScaler
    {
        private readonly HorizontalLayoutScaler _horizontalScaler;
        private readonly VerticalLayoutScaler _verticalScaler;
        
        private bool _isHorizontal = false;
        private bool _labelsVisible = true;
        private bool _nextTimeVisible = true;
        private bool _headerVisible = true;

        // UI Elements references
        private readonly Grid _timersContainer;
        private readonly Grid _mainGrid;
        private readonly Border _timer10mBorder;
        private readonly Border _timer2hBorder;
        private readonly TextBlock _countdown10mText;
        private readonly TextBlock _countdown2hText;
        private readonly TextBlock _nextTime10mText;
        private readonly TextBlock _nextTime2hText;
        private readonly TextBlock _label10m;
        private readonly TextBlock _label2h;
        private readonly System.Windows.Media.ScaleTransform _label10mScale;
        private readonly System.Windows.Media.ScaleTransform _label2hScale;
        private readonly System.Windows.Media.ScaleTransform _nextTime10mScale;
        private readonly System.Windows.Media.ScaleTransform _nextTime2hScale;
        private readonly System.Windows.Media.ScaleTransform _headerScale;
        private readonly TextBlock _headerText;

        public TimerLayoutScaler(
            Grid timersContainer,
            Grid mainGrid,
            Border timer10mBorder,
            Border timer2hBorder,
            TextBlock countdown10mText,
            TextBlock countdown2hText,
            TextBlock nextTime10mText,
            TextBlock nextTime2hText,
            TextBlock label10m,
            TextBlock label2h,
            System.Windows.Media.ScaleTransform label10mScale,
            System.Windows.Media.ScaleTransform label2hScale,
            System.Windows.Media.ScaleTransform nextTime10mScale,
            System.Windows.Media.ScaleTransform nextTime2hScale,
            System.Windows.Media.ScaleTransform headerScale,
            TextBlock headerText)
        {
            _timersContainer = timersContainer;
            _mainGrid = mainGrid;
            _timer10mBorder = timer10mBorder;
            _timer2hBorder = timer2hBorder;
            _countdown10mText = countdown10mText;
            _countdown2hText = countdown2hText;
            _nextTime10mText = nextTime10mText;
            _nextTime2hText = nextTime2hText;
            _label10m = label10m;
            _label2h = label2h;
            _label10mScale = label10mScale;
            _label2hScale = label2hScale;
            _nextTime10mScale = nextTime10mScale;
            _nextTime2hScale = nextTime2hScale;
            _headerScale = headerScale;
            _headerText = headerText;

            _horizontalScaler = new HorizontalLayoutScaler();
            _verticalScaler = new VerticalLayoutScaler();
        }

        public void InitializeLayout()
        {
            // Domyślnie układ pionowy
            _timersContainer.RowDefinitions.Clear();
            _timersContainer.ColumnDefinitions.Clear();

            _timersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(_timer10mBorder, 0);
            Grid.SetColumn(_timer10mBorder, 0);
            Grid.SetRow(_timer2hBorder, 1);
            Grid.SetColumn(_timer2hBorder, 0);

            _isHorizontal = false;

            Console.WriteLine("🎬 [TimerLayoutScaler] Layout initialized (vertical)");
        }

        public void AdaptLayout(double width, double height, Action<Action> dispatcherInvoke)
        {
            // Header visibility z animacją
            bool shouldShowHeader = height >= 150;
            if (shouldShowHeader != _headerVisible)
            {
                AnimateHeaderVisibility(shouldShowHeader);
                _headerVisible = shouldShowHeader;

                // Po zmianie headera poczekaj chwilę i przelicz layout
                dispatcherInvoke(() =>
                {
                    // Wymuś ponowne obliczenie layoutu
                    _timersContainer.InvalidateMeasure();
                    _timersContainer.UpdateLayout();

                    // Przelicz wysokość kontenera
                    double containerHeight = _isHorizontal ? _timersContainer.ActualHeight : _timersContainer.ActualHeight / 2;
                    AdaptTimerElements(containerHeight);
                    AdjustFontSizes(width, height, containerHeight);
                });
            }

            // Dostosuj margines głównego grida
            _mainGrid.Margin = height < 150 ? new Thickness(5) : new Thickness(20);

            // Oblicz proporcję width/height
            double aspectRatio = width / height;
            bool shouldBeHorizontal = aspectRatio >= 1.0;

            // Animuj zmianę orientacji tylko gdy się zmienia
            if (shouldBeHorizontal != _isHorizontal)
            {
                AnimateOrientationChange(shouldBeHorizontal);
                _isHorizontal = shouldBeHorizontal;
            }

            double containerHeight = _isHorizontal ? _timersContainer.ActualHeight : _timersContainer.ActualHeight / 2;

            // Adaptuj elementy z animacją
            AdaptTimerElements(containerHeight);

            // Dostosuj rozmiary czcionek
            AdjustFontSizes(width, height, containerHeight);
        }

        private void AnimateHeaderVisibility(bool show)
        {
            var scaleAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            _headerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            _headerText.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void AnimateOrientationChange(bool toHorizontal)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                // Zmień layout
                _timersContainer.RowDefinitions.Clear();
                _timersContainer.ColumnDefinitions.Clear();

                if (toHorizontal)
                {
                    // Layout poziomy
                    _timersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    _timersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_timer10mBorder, 0);
                    Grid.SetColumn(_timer10mBorder, 0);
                    Grid.SetRow(_timer2hBorder, 0);
                    Grid.SetColumn(_timer2hBorder, 1);
                }
                else
                {
                    // Layout pionowy
                    _timersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_timer10mBorder, 0);
                    Grid.SetColumn(_timer10mBorder, 0);
                    Grid.SetRow(_timer2hBorder, 1);
                    Grid.SetColumn(_timer2hBorder, 0);
                }

                // Fade in
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                _timersContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            _timersContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void AdaptTimerElements(double containerHeight)
        {
            // Labels - pojawiają się od wysokości 100
            bool shouldShowLabels = containerHeight >= 100;
            if (shouldShowLabels != _labelsVisible)
            {
                AnimateElementVisibility(_label10mScale, _label10m, shouldShowLabels);
                AnimateElementVisibility(_label2hScale, _label2h, shouldShowLabels);
                _labelsVisible = shouldShowLabels;
            }

            // Next Time - pojawiają się od wysokości 60
            bool shouldShowNextTime = containerHeight >= 60;
            if (shouldShowNextTime != _nextTimeVisible)
            {
                AnimateElementVisibility(_nextTime10mScale, _nextTime10mText, shouldShowNextTime);
                AnimateElementVisibility(_nextTime2hScale, _nextTime2hText, shouldShowNextTime);
                _nextTimeVisible = shouldShowNextTime;
            }
        }

        private void AnimateElementVisibility(System.Windows.Media.ScaleTransform scale, FrameworkElement element, bool show)
        {
            var scaleAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void AdjustFontSizes(double availableWidth, double availableHeight, double containerHeight)
        {
            ScalingResult result;

            if (_isHorizontal)
            {
                result = _horizontalScaler.CalculateScaling(availableWidth, availableHeight);
            }
            else
            {
                result = _verticalScaler.CalculateScaling(availableWidth, availableHeight, containerHeight, _labelsVisible, _nextTimeVisible);
            }

            // Zastosuj wyniki
            ApplyScalingResult(result);

            // Log
            Console.WriteLine("________________________________________");
            Console.WriteLine($"Size: Container={_timersContainer.ActualHeight:F0}x{_timersContainer.ActualWidth:F0}");
            Console.WriteLine($"Ratio: W/H={availableWidth/availableHeight:F2}, Layout={(_isHorizontal ? "horizontal" : "vertical")}");
            Console.WriteLine($"Fonts: countdown={result.CountdownSize:F1}, label={result.LabelSize:F1}, margin={result.BorderMargin:F1}, padding={result.BorderPadding:F1}, containerH={containerHeight:F0}");
        }

        private void ApplyScalingResult(ScalingResult result)
        {
            // Zastosuj rozmiary czcionek
            _countdown10mText.FontSize = result.CountdownSize;
            _nextTime10mText.FontSize = result.NextTimeSize;
            _label10m.FontSize = result.LabelSize;

            _countdown2hText.FontSize = result.CountdownSize;
            _nextTime2hText.FontSize = result.NextTimeSize;
            _label2h.FontSize = result.LabelSize;

            // Zastosuj marginesy i padding
            var margin = new Thickness(result.BorderMargin);
            var padding = new Thickness(result.BorderPadding);

            _timer10mBorder.Margin = margin;
            _timer10mBorder.Padding = padding;
            _timer2hBorder.Margin = margin;
            _timer2hBorder.Padding = padding;

            // Dostosuj marginesy między elementami
            double innerMargin = Math.Max(2, result.LabelSize * 0.3);

            _label10m.Margin = new Thickness(0, 0, 0, _labelsVisible ? innerMargin : 0);
            _label2h.Margin = new Thickness(0, 0, 0, _labelsVisible ? innerMargin : 0);

            _nextTime10mText.Margin = new Thickness(0, _nextTimeVisible ? innerMargin : 0, 0, 0);
            _nextTime2hText.Margin = new Thickness(0, _nextTimeVisible ? innerMargin : 0, 0, 0);

            _countdown10mText.Margin = new Thickness(Math.Max(1, result.BorderPadding * 0.2));
            _countdown2hText.Margin = new Thickness(Math.Max(1, result.BorderPadding * 0.2));
        }
    }
}