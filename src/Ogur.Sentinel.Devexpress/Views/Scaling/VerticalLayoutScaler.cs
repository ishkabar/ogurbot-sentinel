using System;

namespace Ogur.Sentinel.Devexpress.Views.Scaling
{
    public class VerticalLayoutScaler
    {
        public ScalingResult CalculateScaling(double availableWidth, double availableHeight, double containerHeight, bool labelsVisible, bool nextTimeVisible)
        {
            double panelHeight = availableHeight / 2;
            double minDimension = Math.Min(availableWidth, panelHeight);

            double countdownSize, borderMargin, borderPadding;

            // Gdy elementy są ukryte, countdown może być większy
            if (containerHeight < 150)
            {
                countdownSize = Math.Max(20, Math.Min(80, minDimension * 0.40));
                // Mniejsze marginesy gdy countdown jest duży
                borderMargin = Math.Max(1, Math.Min(3, minDimension * 0.008));
                borderPadding = Math.Max(2, Math.Min(8, minDimension * 0.015));
            }
            else
            {
                countdownSize = Math.Max(16, Math.Min(60, minDimension * 0.25));
                borderMargin = Math.Max(2, Math.Min(5, minDimension * 0.012));
                borderPadding = Math.Max(3, Math.Min(12, minDimension * 0.025));
            }

            double nextTimeSize = Math.Max(8, Math.Min(14, minDimension * 0.04));
            double labelSize = Math.Max(9, Math.Min(16, minDimension * 0.045));

            // Oblicz całkowitą wysokość zawartości
            double totalContentHeight = 0;

            if (labelsVisible)
                totalContentHeight += labelSize * 1.5; // Label + margin

            totalContentHeight += countdownSize * 1.2; // Countdown

            if (nextTimeVisible)
                totalContentHeight += nextTimeSize * 1.8; // NextTime + margin

            totalContentHeight += borderPadding * 2; // Padding góra + dół

            // Jeśli zawartość nie mieści się, zmniejsz czcionki proporcjonalnie
            double availableSpace = availableHeight / 2;

            if (totalContentHeight > availableSpace * 0.95) // 95% dostępnej przestrzeni
            {
                double scaleFactor = (availableSpace * 0.95) / totalContentHeight;
                countdownSize *= scaleFactor;
                nextTimeSize *= scaleFactor;
                labelSize *= scaleFactor;
                borderPadding *= scaleFactor;
                borderMargin *= scaleFactor;

                Console.WriteLine($"⚠️ Scaling down: factor={scaleFactor:F2}, content={totalContentHeight:F0}, available={availableSpace:F0}");
            }

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