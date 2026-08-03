using System;
using System.Drawing;

namespace HZCYKJTHardWare.Proxy.Preview
{
    internal enum PreviewScaleMode
    {
        Stretch,
        Contain,
        Cover
    }

    internal static class PreviewLayoutMath
    {
        public static Rectangle CalculateVideoBounds(
            Size sourceSize,
            Size hostSize,
            PreviewScaleMode scaleMode)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0 ||
                hostSize.Width <= 0 || hostSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            if (scaleMode == PreviewScaleMode.Stretch)
                return new Rectangle(Point.Empty, hostSize);

            var scaleX = (double)hostSize.Width / sourceSize.Width;
            var scaleY = (double)hostSize.Height / sourceSize.Height;
            var scale = scaleMode == PreviewScaleMode.Contain
                ? Math.Min(scaleX, scaleY)
                : Math.Max(scaleX, scaleY);

            var width = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
            var height = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));

            return new Rectangle(
                (hostSize.Width - width) / 2,
                (hostSize.Height - height) / 2,
                width,
                height);
        }
    }
}
