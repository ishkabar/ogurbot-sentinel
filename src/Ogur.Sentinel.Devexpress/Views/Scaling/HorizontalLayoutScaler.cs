using System;

namespace Ogur.Sentinel.Devexpress.Views.Scaling
{
    public class HorizontalLayoutScaler
    {
        private readonly Config.ScalingConfig _config;

        public HorizontalLayoutScaler(Config.ScalingConfig config)
        {
            _config = config;
        }

        public ScalingResult CalculateScaling(double availableWidth, double availableHeight)
        {
            double panelWidth = availableWidth / 2;
            double minDimension = Math.Min(panelWidth, availableHeight);

            double countdownSize = Math.Max(
                _config.Horizontal.Fonts.CountdownMin, 
                Math.Min(_config.Horizontal.Fonts.CountdownMax, minDimension * _config.Horizontal.Fonts.CountdownScale)
            );
            
            double nextTimeSize = Math.Max(
                _config.Horizontal.Fonts.NextTimeMin, 
                Math.Min(_config.Horizontal.Fonts.NextTimeMax, minDimension * _config.Horizontal.Fonts.NextTimeScale)
            );
            
            double labelSize = Math.Max(
                _config.Horizontal.Fonts.LabelMin, 
                Math.Min(_config.Horizontal.Fonts.LabelMax, minDimension * _config.Horizontal.Fonts.LabelScale)
            );

            // Marginesy i padding proporcjonalne do rozmiaru
            double borderMargin = Math.Max(
                _config.Horizontal.Spacing.BorderMarginMin, 
                Math.Min(_config.Horizontal.Spacing.BorderMarginMax, minDimension * _config.Horizontal.Spacing.BorderMarginScale)
            );
            
            double borderPadding = Math.Max(
                _config.Horizontal.Spacing.BorderPaddingMin, 
                Math.Min(_config.Horizontal.Spacing.BorderPaddingMax, minDimension * _config.Horizontal.Spacing.BorderPaddingScale)
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