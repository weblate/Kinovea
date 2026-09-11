using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinovea.Video
{
    public static class FitHelper
    {
        /// <summary>
        /// Fit the payload to the container.
        /// </summary>
        public static Size Fit(Size source, Size bounds, bool allowUpscale)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return source;

            double scaleX = (double)bounds.Width / source.Width;
            double scaleY = (double)bounds.Height / source.Height;
            double scale = Math.Min(scaleX, scaleY);

            if (!allowUpscale)
                scale = Math.Min(scale, 1.0);

            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));

            return new Size(width, height);
        }
        /// <summary>
        /// Get the multiplier going from the simplified ratio to the reference size.
        /// Example: 1920x1080 -> 16x9, multiplier = 120.
        /// </summary>
        public static void GetReferenceMultiplier(Size reference, out int ratioWidth, out int ratioHeight, out int referenceMultiplier)
        {
            int gcd = Gcd(reference.Width, reference.Height);

            ratioWidth = reference.Width / gcd;
            ratioHeight = reference.Height / gcd;
            referenceMultiplier = gcd;
        }

        /// <summary>
        /// Return the multiplier that will fit the ratio width/height (ex: 16x9) into the bounds,
        /// keeping the same aspect ratio and producing an even size.
        /// </summary>
        public static int GetFitEvenMultiplier(Size bounds, int ratioWidth, int ratioHeight)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return 0;

            int multiplier = Math.Min(bounds.Width / ratioWidth, bounds.Height / ratioHeight);

            // The reduced ratio cannot have both dimensions even.
            // Therefore an even multiplier guarantees both final dimensions are even.
            return multiplier & ~1;
        }

        /// <summary>
        /// Return the minimum even multiplier that produces a size >= minSize in both dimensions.
        /// </summary>
        public static int GetMinimumMultiplier(int ratioWidth, int ratioHeight, int minSize)
        {
            int multiplier = Math.Max(
                (minSize + ratioWidth - 1) / ratioWidth,
                (minSize + ratioHeight - 1) / ratioHeight);

            return (multiplier + 1) & ~1;
        }

        /// <summary>
        /// Return the nearest even multiplier to the given multiplier, but at least 2.
        /// </summary>
        public static int GetNearestEvenMultiplier(double multiplier)
        {
            return Math.Max(2, (int)Math.Round(multiplier / 2.0, MidpointRounding.AwayFromZero) * 2);
        }


        private static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }

            return a;
        }

    }


}
