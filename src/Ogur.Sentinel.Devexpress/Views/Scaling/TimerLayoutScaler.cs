using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.Views.Scaling
{
    public class TimerLayoutScaler
    {
        private readonly HorizontalLayoutScaler _horizontalScaler;
        private readonly VerticalLayoutScaler _verticalScaler;
        private readonly ScalingConfig _config;
        
        private bool _isHorizontal = false;
        private bool _labelsVisible = true;
        private bool _nextTimeVisible = true;
        private bool _headerVisible = true;
        private bool _statusTextVisible = true;
        private int _adjustFontSizesCallCount = 0;

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
        private readonly TextBlock _statusText;
        private readonly System.Windows.Media.ScaleTransform _statusTextScale;

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
            TextBlock headerText,
            TextBlock statusText,
            System.Windows.Media.ScaleTransform statusTextScale,
            ScalingConfig config)
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
            _statusText = statusText;
            _statusTextScale = statusTextScale;
            _config = config;

            _horizontalScaler = new HorizontalLayoutScaler(config);
            _verticalScaler = new VerticalLayoutScaler(config);
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
            
            // ✅ Upewnij się że wszystkie elementy są widoczne na start
            _label10m.Visibility = Visibility.Visible;
            _label2h.Visibility = Visibility.Visible;
            _nextTime10mText.Visibility = Visibility.Visible;
            _nextTime2hText.Visibility = Visibility.Visible;
            _headerText.Visibility = Visibility.Visible;
            _statusText.Visibility = Visibility.Visible;
            
            // Ustaw pełną opacity i scale
            _label10m.Opacity = 1;
            _label2h.Opacity = 1;
            _nextTime10mText.Opacity = 1;
            _nextTime2hText.Opacity = 1;
            _headerText.Opacity = 1;
            _statusText.Opacity = 1;
            
            _label10mScale.ScaleY = 1;
            _label2hScale.ScaleY = 1;
            _nextTime10mScale.ScaleY = 1;
            _nextTime2hScale.ScaleY = 1;
            _headerScale.ScaleY = 1;
            _statusTextScale.ScaleY = 1;

            if (App.DebugMode) Console.WriteLine("🎬 [TimerLayoutScaler] Layout initialized (vertical)");
            if (App.DebugMode) Console.WriteLine($"   Label10m.Visibility={_label10m.Visibility}, StatusText.Visibility={_statusText.Visibility}");
        }

        public void AdaptLayout(double width, double height, Action<Action> dispatcherInvoke)
        {
            if (App.DebugMode) Console.WriteLine();
            if (App.DebugMode) Console.WriteLine("🔄 ADAPT LAYOUT CALLED");
            if (App.DebugMode) Console.WriteLine($"   Input: width={width:F1}, height={height:F1}");
            if (App.DebugMode) Console.WriteLine($"   Current state: isHorizontal={_isHorizontal}, headerVisible={_headerVisible}");
            
            // Header visibility z animacją
            bool shouldShowHeader = height >= _config.General.HeaderHeightThreshold;
            if (App.DebugMode) Console.WriteLine($"   Header check: height ({height:F1}) >= threshold ({_config.General.HeaderHeightThreshold}) = {shouldShowHeader}");
            
            if (shouldShowHeader != _headerVisible)
            {
                if (App.DebugMode) Console.WriteLine($"   ⚡ HEADER VISIBILITY CHANGE: {_headerVisible} → {shouldShowHeader}");
                AnimateHeaderVisibility(shouldShowHeader);
                _headerVisible = shouldShowHeader;

                dispatcherInvoke(() =>
                {
                    if (App.DebugMode) Console.WriteLine("   📐 Recalculating layout after header change...");
                    _timersContainer.InvalidateMeasure();
                    _timersContainer.UpdateLayout();

                    double containerHeight = _isHorizontal ? _timersContainer.ActualHeight : _timersContainer.ActualHeight / 2;
                    AdaptTimerElements(containerHeight);
                    AdjustFontSizes(width, height, containerHeight);
                });
            }

            // ✅ StatusText visibility (podobnie jak Header)
            bool shouldShowStatusText = height >= _config.StatusText.HeightThreshold;
            if (App.DebugMode) Console.WriteLine($"   StatusText check: height ({height:F1}) >= threshold ({_config.StatusText.HeightThreshold}) = {shouldShowStatusText}");
            
            if (shouldShowStatusText != _statusTextVisible)
            {
                if (App.DebugMode) Console.WriteLine($"   ⚡ STATUSTEXT VISIBILITY CHANGE: {_statusTextVisible} → {shouldShowStatusText}");
                AnimateElementVisibility(_statusTextScale, _statusText, shouldShowStatusText);
                _statusTextVisible = shouldShowStatusText;
            }

            // Dostosuj margines głównego grida
            _mainGrid.Margin = height < _config.General.HeaderHeightThreshold 
                ? new Thickness(_config.Margins.MainGridMarginSmall) 
                : new Thickness(_config.Margins.MainGridMarginLarge);

            // Oblicz proporcję width/height
            double aspectRatio = width / height;
            bool shouldBeHorizontal = aspectRatio >= _config.General.OrientationThreshold;
            if (App.DebugMode) Console.WriteLine($"   Orientation check: aspectRatio ({aspectRatio:F2}) >= threshold ({_config.General.OrientationThreshold}) = {shouldBeHorizontal}");

            // Animuj zmianę orientacji tylko gdy się zmienia
            if (shouldBeHorizontal != _isHorizontal)
            {
                if (App.DebugMode) Console.WriteLine($"   ⚡ ORIENTATION CHANGE: {(_isHorizontal ? "horizontal" : "vertical")} → {(shouldBeHorizontal ? "horizontal" : "vertical")}");
                AnimateOrientationChange(shouldBeHorizontal);
                _isHorizontal = shouldBeHorizontal;
            }

            double containerHeight = _isHorizontal ? _timersContainer.ActualHeight : _timersContainer.ActualHeight / 2;
            if (App.DebugMode) Console.WriteLine($"   ContainerHeight calculation: {(_isHorizontal ? "full height" : "half height")} = {containerHeight:F1}");

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
                Duration = TimeSpan.FromMilliseconds(_config.Animations.HeaderAnimationDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(_config.Animations.HeaderAnimationDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            if (show)
            {
                _headerText.Visibility = Visibility.Visible;
            }
            else
            {
                opacityAnim.Completed += (s, e) =>
                {
                    _headerText.Visibility = Visibility.Collapsed;
                };
            }

            _headerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            _headerText.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void AnimateOrientationChange(bool toHorizontal)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(_config.Animations.OrientationAnimationDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                _timersContainer.RowDefinitions.Clear();
                _timersContainer.ColumnDefinitions.Clear();

                if (toHorizontal)
                {
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
                    _timersContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    _timersContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_timer10mBorder, 0);
                    Grid.SetColumn(_timer10mBorder, 0);
                    Grid.SetRow(_timer2hBorder, 1);
                    Grid.SetColumn(_timer2hBorder, 0);
                }

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(_config.Animations.OrientationAnimationDurationMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                _timersContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            _timersContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void AdaptTimerElements(double containerHeight)
        {
            if (App.DebugMode) Console.WriteLine();
            if (App.DebugMode) Console.WriteLine("👁️ ADAPT TIMER ELEMENTS");
            if (App.DebugMode) Console.WriteLine($"   ContainerHeight: {containerHeight:F1}");
            
            // Labels - pojawiają się od wysokości z config
            bool shouldShowLabels = containerHeight >= _config.General.LabelHeightThreshold;
            if (App.DebugMode) Console.WriteLine($"   Labels check: containerH ({containerHeight:F1}) >= threshold ({_config.General.LabelHeightThreshold}) = {shouldShowLabels}");
            
            if (shouldShowLabels != _labelsVisible)
            {
                if (App.DebugMode) Console.WriteLine($"   ⚡ LABELS VISIBILITY CHANGE: {_labelsVisible} → {shouldShowLabels}");
                AnimateElementVisibility(_label10mScale, _label10m, shouldShowLabels);
                AnimateElementVisibility(_label2hScale, _label2h, shouldShowLabels);
                _labelsVisible = shouldShowLabels;
            }

            // Next Time - pojawiają się od wysokości z config
            bool shouldShowNextTime = containerHeight >= _config.General.NextTimeHeightThreshold;
            if (App.DebugMode) Console.WriteLine($"   NextTime check: containerH ({containerHeight:F1}) >= threshold ({_config.General.NextTimeHeightThreshold}) = {shouldShowNextTime}");
            
            if (shouldShowNextTime != _nextTimeVisible)
            {
                if (App.DebugMode) Console.WriteLine($"   ⚡ NEXTTIME VISIBILITY CHANGE: {_nextTimeVisible} → {shouldShowNextTime}");
                AnimateElementVisibility(_nextTime10mScale, _nextTime10mText, shouldShowNextTime);
                AnimateElementVisibility(_nextTime2hScale, _nextTime2hText, shouldShowNextTime);
                _nextTimeVisible = shouldShowNextTime;
            }


        }

        private void AnimateElementVisibility(System.Windows.Media.ScaleTransform scale, FrameworkElement element, bool show)
        {
            if (App.DebugMode) Console.WriteLine($"      🎬 Animating element '{element.Name}': {(show ? "SHOW" : "HIDE")}");
            
            var scaleAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(_config.Animations.ElementVisibilityAnimationDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(_config.Animations.ElementVisibilityAnimationDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            if (show)
            {
                if (App.DebugMode) Console.WriteLine($"         Setting Visibility.Visible first");
                element.Visibility = Visibility.Visible;
            }
            else
            {
                opacityAnim.Completed += (s, e) =>
                {
                    if (App.DebugMode) Console.WriteLine($"         Animation complete, setting Visibility.Collapsed");
                    element.Visibility = Visibility.Collapsed;
                };
            }

            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void AdjustFontSizes(double availableWidth, double availableHeight, double containerHeight)
        {
            _adjustFontSizesCallCount++;
            
            if (_adjustFontSizesCallCount % 2 == 0)
            {
                if (App.DebugMode) Console.Clear();
            }
            
            ScalingResult result;

            if (App.DebugMode) Console.WriteLine("========================================");
            if (App.DebugMode) Console.WriteLine($"🔧 FONT SIZING CALCULATION #{_adjustFontSizesCallCount}");
            if (App.DebugMode) Console.WriteLine("========================================");
            if (App.DebugMode) Console.WriteLine($"📐 Input dimensions:");
            if (App.DebugMode) Console.WriteLine($"   availableWidth={availableWidth:F1}, availableHeight={availableHeight:F1}");
            if (App.DebugMode) Console.WriteLine($"   containerHeight={containerHeight:F1}");
            if (App.DebugMode) Console.WriteLine($"   aspectRatio={availableWidth/availableHeight:F2}");
            if (App.DebugMode) Console.WriteLine($"   isHorizontal={_isHorizontal}");
            if (App.DebugMode) Console.WriteLine();

            if (_isHorizontal)
            {
                if (App.DebugMode) Console.WriteLine("🔀 HORIZONTAL LAYOUT MODE");
                double panelWidth = availableWidth / 2;
                double minDimension = Math.Min(panelWidth, availableHeight);
                if (App.DebugMode) Console.WriteLine($"   panelWidth={panelWidth:F1}, minDimension={minDimension:F1}");
                if (App.DebugMode) Console.WriteLine();
                
                result = _horizontalScaler.CalculateScaling(availableWidth, availableHeight);
                
                if (App.DebugMode) Console.WriteLine($"📏 Font calculations:");
                if (App.DebugMode) Console.WriteLine($"   countdown: Max({_config.Horizontal.Fonts.CountdownMin}, Min({_config.Horizontal.Fonts.CountdownMax}, {minDimension:F1}*{_config.Horizontal.Fonts.CountdownScale})) = {result.CountdownSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   nextTime:  Max({_config.Horizontal.Fonts.NextTimeMin}, Min({_config.Horizontal.Fonts.NextTimeMax}, {minDimension:F1}*{_config.Horizontal.Fonts.NextTimeScale})) = {result.NextTimeSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   label:     Max({_config.Horizontal.Fonts.LabelMin}, Min({_config.Horizontal.Fonts.LabelMax}, {minDimension:F1}*{_config.Horizontal.Fonts.LabelScale})) = {result.LabelSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   statusText: Max({_config.StatusText.FontMin}, Min({_config.StatusText.FontMax}, {minDimension:F1}*{_config.StatusText.FontScale})) = {result.StatusTextSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   margin:    Max({_config.Horizontal.Spacing.BorderMarginMin}, Min({_config.Horizontal.Spacing.BorderMarginMax}, {minDimension:F1}*{_config.Horizontal.Spacing.BorderMarginScale})) = {result.BorderMargin:F1}");
                if (App.DebugMode) Console.WriteLine($"   padding:   Max({_config.Horizontal.Spacing.BorderPaddingMin}, Min({_config.Horizontal.Spacing.BorderPaddingMax}, {minDimension:F1}*{_config.Horizontal.Spacing.BorderPaddingScale})) = {result.BorderPadding:F1}");
                if (App.DebugMode) Console.WriteLine($"   statusMarginTop: Max({_config.StatusText.MarginTopMin}, Min({_config.StatusText.MarginTopMax}, {minDimension:F1}*{_config.StatusText.MarginTopScale})) = {result.StatusTextMarginTop:F1}");
            }
            else
            {
                if (App.DebugMode) Console.WriteLine("🔀 VERTICAL LAYOUT MODE");
                double panelHeight = availableHeight / 2;
                double minDimension = Math.Min(availableWidth, panelHeight);
                bool isCompactMode = containerHeight < _config.General.CompactModeThreshold;
                
                if (App.DebugMode) Console.WriteLine($"   panelHeight={panelHeight:F1}, minDimension={minDimension:F1}");
                if (App.DebugMode) Console.WriteLine($"   compactMode={isCompactMode}");
                if (App.DebugMode) Console.WriteLine();
                
                result = _verticalScaler.CalculateScaling(availableWidth, availableHeight, containerHeight, _labelsVisible, _nextTimeVisible);
                
                if (isCompactMode)
                {
                    if (App.DebugMode) Console.WriteLine($"⚡ COMPACT MODE");
                    if (App.DebugMode) Console.WriteLine($"📏 Font calculations:");
                    if (App.DebugMode) Console.WriteLine($"   countdown: Max({_config.Vertical.CompactMode.CountdownMin}, Min({_config.Vertical.CompactMode.CountdownMax}, {minDimension:F1}*{_config.Vertical.CompactMode.CountdownScale})) = {result.CountdownSize:F1}");
                    if (App.DebugMode) Console.WriteLine($"   margin:    Max({_config.Vertical.CompactMode.BorderMarginMin}, Min({_config.Vertical.CompactMode.BorderMarginMax}, {minDimension:F1}*{_config.Vertical.CompactMode.BorderMarginScale})) = {result.BorderMargin:F1}");
                    if (App.DebugMode) Console.WriteLine($"   padding:   Max({_config.Vertical.CompactMode.BorderPaddingMin}, Min({_config.Vertical.CompactMode.BorderPaddingMax}, {minDimension:F1}*{_config.Vertical.CompactMode.BorderPaddingScale})) = {result.BorderPadding:F1}");
                }
                else
                {
                    if (App.DebugMode) Console.WriteLine($"📏 Font calculations (Normal):");
                    if (App.DebugMode) Console.WriteLine($"   countdown: Max({_config.Vertical.Fonts.CountdownMin}, Min({_config.Vertical.Fonts.CountdownMax}, {minDimension:F1}*{_config.Vertical.Fonts.CountdownScale})) = {result.CountdownSize:F1}");
                    if (App.DebugMode) Console.WriteLine($"   margin:    Max({_config.Vertical.Spacing.BorderMarginMin}, Min({_config.Vertical.Spacing.BorderMarginMax}, {minDimension:F1}*{_config.Vertical.Spacing.BorderMarginScale})) = {result.BorderMargin:F1}");
                    if (App.DebugMode) Console.WriteLine($"   padding:   Max({_config.Vertical.Spacing.BorderPaddingMin}, Min({_config.Vertical.Spacing.BorderPaddingMax}, {minDimension:F1}*{_config.Vertical.Spacing.BorderPaddingScale})) = {result.BorderPadding:F1}");
                }
                
                if (App.DebugMode) Console.WriteLine($"   nextTime:  Max({_config.Vertical.Fonts.NextTimeMin}, Min({_config.Vertical.Fonts.NextTimeMax}, {minDimension:F1}*{_config.Vertical.Fonts.NextTimeScale})) = {result.NextTimeSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   label:     Max({_config.Vertical.Fonts.LabelMin}, Min({_config.Vertical.Fonts.LabelMax}, {minDimension:F1}*{_config.Vertical.Fonts.LabelScale})) = {result.LabelSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   statusText: Max({_config.StatusText.FontMin}, Min({_config.StatusText.FontMax}, {minDimension:F1}*{_config.StatusText.FontScale})) = {result.StatusTextSize:F1}");
                if (App.DebugMode) Console.WriteLine($"   statusMarginTop: Max({_config.StatusText.MarginTopMin}, Min({_config.StatusText.MarginTopMax}, {minDimension:F1}*{_config.StatusText.MarginTopScale})) = {result.StatusTextMarginTop:F1}");
            }

            if (App.DebugMode) Console.WriteLine();
            if (App.DebugMode) Console.WriteLine("👁️ VISIBILITY STATUS:");
            if (App.DebugMode) Console.WriteLine($"   Header:     {_headerVisible}");
            if (App.DebugMode) Console.WriteLine($"   Labels:     {_labelsVisible}");
            if (App.DebugMode) Console.WriteLine($"   NextTime:   {_nextTimeVisible}");
            if (App.DebugMode) Console.WriteLine($"   StatusText: {_statusTextVisible}");
            if (App.DebugMode) Console.WriteLine();

            ApplyScalingResult(result);

            if (App.DebugMode) Console.WriteLine("✅ FINAL APPLIED VALUES:");
            if (App.DebugMode) Console.WriteLine($"   countdown={result.CountdownSize:F1}, label={result.LabelSize:F1}");
            if (App.DebugMode) Console.WriteLine($"   nextTime={result.NextTimeSize:F1}, statusText={result.StatusTextSize:F1}");
            if (App.DebugMode) Console.WriteLine($"   statusMarginTop={result.StatusTextMarginTop:F1}");
            if (App.DebugMode) Console.WriteLine("========================================");
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

            // ✅ StatusText - zastosuj rozmiar i margin
            _statusText.FontSize = result.StatusTextSize;
            _statusText.Margin = new Thickness(0, result.StatusTextMarginTop, 0, 0);

            // Zastosuj marginesy i padding
            var margin = new Thickness(result.BorderMargin);
            var padding = new Thickness(result.BorderPadding);

            _timer10mBorder.Margin = margin;
            _timer10mBorder.Padding = padding;
            _timer2hBorder.Margin = margin;
            _timer2hBorder.Padding = padding;

            // Dostosuj marginesy między elementami
            double innerMargin = Math.Max(_config.Margins.InnerMarginMin, result.LabelSize * _config.Margins.InnerMarginScale);

            _label10m.Margin = new Thickness(0, 0, 0, _labelsVisible ? innerMargin : 0);
            _label2h.Margin = new Thickness(0, 0, 0, _labelsVisible ? innerMargin : 0);

            _nextTime10mText.Margin = new Thickness(0, _nextTimeVisible ? innerMargin : 0, 0, 0);
            _nextTime2hText.Margin = new Thickness(0, _nextTimeVisible ? innerMargin : 0, 0, 0);

            _countdown10mText.Margin = new Thickness(Math.Max(1, result.BorderPadding * _config.Margins.CountdownMarginScale));
            _countdown2hText.Margin = new Thickness(Math.Max(1, result.BorderPadding * _config.Margins.CountdownMarginScale));
        }
    }
}