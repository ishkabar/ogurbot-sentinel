using System;

namespace Ogur.Sentinel.Devexpress.Views.Scaling
{
    public class VerticalLayoutScaler
    {
        private readonly Config.ScalingConfig _config;

        public VerticalLayoutScaler(Config.ScalingConfig config)
        {
            _config = config;
        }

        public ScalingResult CalculateScaling(double availableWidth, double availableHeight, double containerHeight, bool labelsVisible, bool nextTimeVisible)
        {
            double panelHeight = availableHeight / 2;
            double minDimension = Math.Min(availableWidth, panelHeight);

            double countdownSize, borderMargin, borderPadding;

            // Gdy elementy są ukryte, countdown może być większy
            if (containerHeight < _config.General.CompactModeThreshold)
            {
                countdownSize = Math.Max(
                    _config.Vertical.CompactMode.CountdownMin, 
                    Math.Min(_config.Vertical.CompactMode.CountdownMax, minDimension * _config.Vertical.CompactMode.CountdownScale)
                );
                
                // Mniejsze marginesy gdy countdown jest duży
                borderMargin = Math.Max(
                    _config.Vertical.CompactMode.BorderMarginMin, 
                    Math.Min(_config.Vertical.CompactMode.BorderMarginMax, minDimension * _config.Vertical.CompactMode.BorderMarginScale)
                );
                
                borderPadding = Math.Max(
                    _config.Vertical.CompactMode.BorderPaddingMin, 
                    Math.Min(_config.Vertical.CompactMode.BorderPaddingMax, minDimension * _config.Vertical.CompactMode.BorderPaddingScale)
                );
            }
            else
            {
                countdownSize = Math.Max(
                    _config.Vertical.Fonts.CountdownMin, 
                    Math.Min(_config.Vertical.Fonts.CountdownMax, minDimension * _config.Vertical.Fonts.CountdownScale)
                );
                
                borderMargin = Math.Max(
                    _config.Vertical.Spacing.BorderMarginMin, 
                    Math.Min(_config.Vertical.Spacing.BorderMarginMax, minDimension * _config.Vertical.Spacing.BorderMarginScale)
                );
                
                borderPadding = Math.Max(
                    _config.Vertical.Spacing.BorderPaddingMin, 
                    Math.Min(_config.Vertical.Spacing.BorderPaddingMax, minDimension * _config.Vertical.Spacing.BorderPaddingScale)
                );
            }

            double nextTimeSize = Math.Max(
                _config.Vertical.Fonts.NextTimeMin, 
                Math.Min(_config.Vertical.Fonts.NextTimeMax, minDimension * _config.Vertical.Fonts.NextTimeScale)
            );
            
            double labelSize = Math.Max(
                _config.Vertical.Fonts.LabelMin, 
                Math.Min(_config.Vertical.Fonts.LabelMax, minDimension * _config.Vertical.Fonts.LabelScale)
            );

            // ✅ StatusText - obliczenia
            double statusTextSize = Math.Max(
                _config.StatusText.FontMin,
                Math.Min(_config.StatusText.FontMax, minDimension * _config.StatusText.FontScale)
            );

            double statusTextMarginTop = Math.Max(
                _config.StatusText.MarginTopMin,
                Math.Min(_config.StatusText.MarginTopMax, minDimension * _config.StatusText.MarginTopScale)
            );

            // Oblicz całkowitą wysokość zawartości
            double totalContentHeight = 0;

            if (labelsVisible)
                totalContentHeight += labelSize * _config.Overflow.LabelHeightMultiplier; // Label + margin

            totalContentHeight += countdownSize * _config.Overflow.CountdownHeightMultiplier; // Countdown

            if (nextTimeVisible)
                totalContentHeight += nextTimeSize * _config.Overflow.NextTimeHeightMultiplier; // NextTime + margin

            totalContentHeight += borderPadding * 2; // Padding góra + dół

            // Jeśli zawartość nie mieści się, zmniejsz czcionki proporcjonalnie
            double availableSpace = availableHeight / 2;

            if (totalContentHeight > availableSpace * _config.Overflow.Threshold)
            {
                double scaleFactor = (availableSpace * _config.Overflow.Threshold) / totalContentHeight;
                countdownSize *= scaleFactor;
                nextTimeSize *= scaleFactor;
                labelSize *= scaleFactor;
                borderPadding *= scaleFactor;
                borderMargin *= scaleFactor;
                statusTextSize *= scaleFactor;
                statusTextMarginTop *= scaleFactor;

                //Console.WriteLine($"⚠️ Scaling down: factor={scaleFactor:F2}, content={totalContentHeight:F0}, available={availableSpace:F0}");
            }

            return new ScalingResult
            {
                CountdownSize = countdownSize,
                NextTimeSize = nextTimeSize,
                LabelSize = labelSize,
                BorderMargin = borderMargin,
                BorderPadding = borderPadding,
                StatusTextSize = statusTextSize,
                StatusTextMarginTop = statusTextMarginTop
            };
        }
    }
}