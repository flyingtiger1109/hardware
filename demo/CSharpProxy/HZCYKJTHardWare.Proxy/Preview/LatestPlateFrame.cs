using System;

namespace HZCYKJTHardWare.Proxy.Preview
{
    /// <summary>
    /// 已经完成编码并通过校验的最新车牌 JPEG 快照。
    /// Jpeg 只在内部线程安全快照后交给 HTTP 响应，调用方不会持有解码器内存。
    /// </summary>
    internal sealed class LatestPlateFrameSnapshot
    {
        internal LatestPlateFrameSnapshot(byte[] jpeg, int width, int height,
            long sequence, DateTime capturedUtc)
        {
            Jpeg = jpeg ?? throw new ArgumentNullException(nameof(jpeg));
            Width = width;
            Height = height;
            Sequence = sequence;
            CapturedUtc = capturedUtc;
        }

        internal byte[] Jpeg { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal long Sequence { get; }
        internal DateTime CapturedUtc { get; }
    }

    /// <summary>
    /// 不依赖 GDI+ 的 JPEG 最小结构校验器，同时提取实际 SOF 帧尺寸。
    /// </summary>
    internal static class JpegFrameValidator
    {
        internal static bool TryGetDimensions(byte[] data, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (data == null || data.Length < 4 ||
                data[0] != 0xFF || data[1] != 0xD8 ||
                data[data.Length - 2] != 0xFF || data[data.Length - 1] != 0xD9)
                return false;

            var index = 2;
            while (index + 1 < data.Length)
            {
                if (data[index] != 0xFF)
                    return false;

                while (index < data.Length && data[index] == 0xFF)
                    index++;
                if (index >= data.Length)
                    return false;

                var marker = data[index++];
                if (marker == 0xD9)
                    break;
                if (marker == 0xDA)
                    break;
                if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7))
                    continue;
                if (index + 1 >= data.Length)
                    return false;

                var segmentLength = (data[index] << 8) | data[index + 1];
                if (segmentLength < 2 || index + segmentLength > data.Length)
                    return false;

                if (IsStartOfFrame(marker) && segmentLength >= 7)
                {
                    var frameHeight = (data[index + 3] << 8) | data[index + 4];
                    var frameWidth = (data[index + 5] << 8) | data[index + 6];
                    if (frameWidth <= 0 || frameHeight <= 0)
                        return false;
                    width = frameWidth;
                    height = frameHeight;
                    return true;
                }

                index += segmentLength;
            }

            return width > 0 && height > 0;
        }

        private static bool IsStartOfFrame(byte marker)
        {
            return (marker >= 0xC0 && marker <= 0xC3) ||
                   (marker >= 0xC5 && marker <= 0xC7) ||
                   (marker >= 0xC9 && marker <= 0xCB) ||
                   (marker >= 0xCD && marker <= 0xCF);
        }
    }
}
