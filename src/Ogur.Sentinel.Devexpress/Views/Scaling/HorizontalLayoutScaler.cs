using System;

namespace Ogur.Sentinel.Devexpress.Views.Scaling
{
    public class HorizontalLayoutScaler
    {
        public ScalingResult CalculateScaling(double availableWidth, double availableHeight)
        {
            double panelWidth = availableWidth / 2;
            double minDimension = Math.Min(panelWidth, availableHeight);

            double countdownSize = Math.Max(16, Math.Min(72, minDimension * 0.20));
            double nextTimeSize = Math.Max(8, Math.Min(16, minDimension * 0.04));
            double labelSize = Math.Max(10, Math.Min(18, minDimension * 0.045));

            // Marginesy i padding proporcjonalne do rozmiaru
            double borderMargin = Math.Max(2, Math.Min(8, minDimension * 0.015));
            double borderPadding = Math.Max(3, Math.Min(15, minDimension * 0.03));

            return new ScalingResult
            {
                CountdownSize = countdownSize,
                NextTimeSize = nextTimeSize,
                LabelSize = labelSize,
                BorderMargin = borderMargin,
                BorderPadding = borderPadding
            };
        }
    }
}