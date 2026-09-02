using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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
            : this(jpeg, width, height, "jpeg", sequence, capturedUtc)
        {
        }

        internal LatestPlateFrameSnapshot(byte[] jpeg, int width, int height,
            string format, long sequence, DateTime capturedUtc)
        {
            Jpeg = jpeg ?? throw new ArgumentNullException(nameof(jpeg));
            Width = width;
            Height = height;
            Format = string.IsNullOrWhiteSpace(format) ? "unknown" : format;
            Sequence = sequence;
            CapturedUtc = capturedUtc;
        }

        internal byte[] Jpeg { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal string Format { get; }
        internal long Sequence { get; }
        internal DateTime CapturedUtc { get; }
    }

    /// <summary>
    /// 线程安全的 LastGoodFrame 缓存。发布和读取都复制字节，调用方不能修改缓存内容。
    /// </summary>
    internal sealed class LatestPlateFrameCache
    {
        private readonly object _sync = new object();
        private LatestPlateFrameSnapshot _snapshot;
        private long _nextSequence;

        internal void Publish(byte[] jpeg, int width, int height, string format,
            DateTime capturedUtc)
        {
            if (jpeg == null || jpeg.Length == 0)
                throw new ArgumentException("JPEG数据为空", nameof(jpeg));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "图像尺寸必须大于0");

            var copy = new byte[jpeg.Length];
            Buffer.BlockCopy(jpeg, 0, copy, 0, jpeg.Length);
            lock (_sync)
            {
                _snapshot = new LatestPlateFrameSnapshot(copy, width, height,
                    format, ++_nextSequence, capturedUtc);
            }
        }

        internal bool TryGet(out LatestPlateFrameSnapshot snapshot)
        {
            lock (_sync)
            {
                if (_snapshot == null)
                {
                    snapshot = null;
                    return false;
                }

                var copy = new byte[_snapshot.Jpeg.Length];
                Buffer.BlockCopy(_snapshot.Jpeg, 0, copy, 0, copy.Length);
                snapshot = new LatestPlateFrameSnapshot(copy, _snapshot.Width,
                    _snapshot.Height, _snapshot.Format, _snapshot.Sequence,
                    _snapshot.CapturedUtc);
                return true;
            }
        }

        internal bool TryGetCapturedUtc(out DateTime capturedUtc)
        {
            lock (_sync)
            {
                if (_snapshot == null)
                {
                    capturedUtc = default(DateTime);
                    return false;
                }

                capturedUtc = _snapshot.CapturedUtc;
                return true;
            }
        }

        internal bool TryGetSequence(out long sequence)
        {
            lock (_sync)
            {
                if (_snapshot == null)
                {
                    sequence = 0;
                    return false;
                }

                sequence = _snapshot.Sequence;
                return true;
            }
        }

        internal void Clear()
        {
            lock (_sync)
            {
                _snapshot = null;
            }
        }
    }

    /// <summary>
    /// 等待快照文件创建且连续两次观测到大小/写入时间稳定后再读取。
    /// </summary>
    internal static class SnapshotFileReader
    {
        internal static bool TryReadStable(string path, int maxBytes, int timeoutMs,
            Func<bool> isCancellationRequested, out byte[] data, out long fileBytes,
            out string failureReason)
        {
            data = null;
            fileBytes = 0;
            failureReason = "snapshot_file_missing";
            if (string.IsNullOrWhiteSpace(path))
            {
                failureReason = "snapshot_file_path_invalid";
                return false;
            }

            var waitMs = Math.Max(1, timeoutMs);
            var stopwatch = Stopwatch.StartNew();
            long lastLength = -1;
            DateTime lastWriteUtc = DateTime.MinValue;
            var stableSamples = 0;

            while (stopwatch.ElapsedMilliseconds <= waitMs)
            {
                if (isCancellationRequested != null && isCancellationRequested())
                {
                    failureReason = "snapshot_cancelled";
                    return false;
                }

                try
                {
                    var fileInfo = new FileInfo(path);
                    fileInfo.Refresh();
                    if (!fileInfo.Exists)
                    {
                        failureReason = "snapshot_file_missing";
                        stableSamples = 0;
                        SleepUntilNextSample(stopwatch, waitMs);
                        continue;
                    }

                    fileBytes = fileInfo.Length;
                    if (fileBytes <= 0)
                    {
                        failureReason = "snapshot_file_empty";
                        stableSamples = 0;
                        SleepUntilNextSample(stopwatch, waitMs);
                        continue;
                    }
                    if (fileBytes > maxBytes || fileBytes > int.MaxValue)
                    {
                        failureReason = "snapshot_file_too_large";
                        return false;
                    }

                    if (fileBytes == lastLength &&
                        fileInfo.LastWriteTimeUtc == lastWriteUtc)
                        stableSamples++;
                    else
                        stableSamples = 1;

                    lastLength = fileBytes;
                    lastWriteUtc = fileInfo.LastWriteTimeUtc;
                    if (stableSamples < 2)
                    {
                        SleepUntilNextSample(stopwatch, waitMs);
                        continue;
                    }

                    var buffer = new byte[(int)fileBytes];
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    {
                        var offset = 0;
                        while (offset < buffer.Length)
                        {
                            var read = stream.Read(buffer, offset, buffer.Length - offset);
                            if (read <= 0)
                                break;
                            offset += read;
                        }

                        if (offset != buffer.Length)
                        {
                            failureReason = "snapshot_file_incomplete";
                            stableSamples = 0;
                            SleepUntilNextSample(stopwatch, waitMs);
                            continue;
                        }
                    }

                    fileInfo.Refresh();
                    if (fileInfo.Length != fileBytes ||
                        fileInfo.LastWriteTimeUtc != lastWriteUtc)
                    {
                        failureReason = "snapshot_file_changed_during_read";
                        stableSamples = 0;
                        SleepUntilNextSample(stopwatch, waitMs);
                        continue;
                    }

                    data = buffer;
                    return true;
                }
                catch (IOException)
                {
                    failureReason = "snapshot_file_read_failed";
                    stableSamples = 0;
                    SleepUntilNextSample(stopwatch, waitMs);
                }
                catch (UnauthorizedAccessException)
                {
                    failureReason = "snapshot_file_access_denied";
                    stableSamples = 0;
                    SleepUntilNextSample(stopwatch, waitMs);
                }
            }

            return false;
        }

        private static void SleepUntilNextSample(Stopwatch stopwatch, int timeoutMs)
        {
            var remaining = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
            if (remaining > 0)
                Thread.Sleep(Math.Min(20, remaining));
        }
    }

    /// <summary>
    /// 识别实际图片格式并统一转换为可供 Native/Delphi 使用的 JPEG。
    /// 文件扩展名不参与判断。
    /// </summary>
    internal static class SnapshotImageNormalizer
    {
        internal static bool TryNormalizeToJpeg(byte[] data, out byte[] jpeg,
            out string detectedFormat, out int width, out int height,
            out string failureReason)
        {
            jpeg = null;
            detectedFormat = DetectFormat(data);
            width = 0;
            height = 0;
            failureReason = null;
            if (data == null || data.Length == 0)
            {
                failureReason = "snapshot_empty";
                return false;
            }

            try
            {
                using (var input = new MemoryStream(data, false))
                using (var source = Image.FromStream(input, false, true))
                {
                    width = source.Width;
                    height = source.Height;
                    if (width <= 0 || height <= 0)
                    {
                        failureReason = "snapshot_dimension_invalid";
                        return false;
                    }

                    var sourceFormat = GetImageFormatName(source.RawFormat);
                    if (detectedFormat == "unknown" && sourceFormat != "unknown")
                        detectedFormat = sourceFormat;

                    int jpegWidth;
                    int jpegHeight;
                    if (detectedFormat == "jpeg" &&
                        JpegFrameValidator.TryGetDimensions(data, out jpegWidth, out jpegHeight))
                    {
                        jpeg = new byte[data.Length];
                        Buffer.BlockCopy(data, 0, jpeg, 0, data.Length);
                        width = jpegWidth;
                        height = jpegHeight;
                    }
                    else
                    {
                        using (var bitmap = new Bitmap(width, height,
                            PixelFormat.Format24bppRgb))
                        using (var graphics = Graphics.FromImage(bitmap))
                        using (var output = new MemoryStream())
                        {
                            graphics.Clear(Color.Black);
                            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                            bitmap.Save(output, ImageFormat.Jpeg);
                            jpeg = output.ToArray();
                        }
                    }
                }

                if (jpeg == null || jpeg.Length == 0 ||
                    !JpegFrameValidator.TryGetDimensions(jpeg, out var outputWidth,
                        out var outputHeight))
                {
                    jpeg = null;
                    width = 0;
                    height = 0;
                    failureReason = "snapshot_jpeg_encode_invalid";
                    return false;
                }

                width = outputWidth;
                height = outputHeight;
                return true;
            }
            catch (ArgumentException)
            {
                failureReason = "snapshot_decode_failed";
                jpeg = null;
                width = 0;
                height = 0;
                return false;
            }
            catch (ExternalException)
            {
                failureReason = "snapshot_gdiplus_failed";
                jpeg = null;
                width = 0;
                height = 0;
                return false;
            }
            catch (OutOfMemoryException)
            {
                failureReason = "snapshot_decode_out_of_memory";
                jpeg = null;
                width = 0;
                height = 0;
                return false;
            }
        }

        internal static string DetectFormat(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "unknown";
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
                return "jpeg";
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 &&
                data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D &&
                data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
                return "png";
            if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
                return "bmp";
            if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 &&
                data[2] == 0x46 && data[3] == 0x38)
                return "gif";
            if (data.Length >= 4 && data[0] == 0x49 && data[1] == 0x49 &&
                data[2] == 0x2A && data[3] == 0x00)
                return "tiff";
            if (data.Length >= 4 && data[0] == 0x4D && data[1] == 0x4D &&
                data[2] == 0x00 && data[3] == 0x2A)
                return "tiff";
            return "unknown";
        }

        private static string GetImageFormatName(ImageFormat format)
        {
            if (format == null)
                return "unknown";
            if (format.Guid == ImageFormat.Jpeg.Guid)
                return "jpeg";
            if (format.Guid == ImageFormat.Png.Guid)
                return "png";
            if (format.Guid == ImageFormat.Bmp.Guid)
                return "bmp";
            if (format.Guid == ImageFormat.Gif.Guid)
                return "gif";
            if (format.Guid == ImageFormat.Tiff.Guid)
                return "tiff";
            return "unknown";
        }
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
