using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Storage
{
    public static class FileSaver
    {
        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileWriteThrough = 0x8;
        private const int ErrorAccessDenied = 5;
        private const int ErrorSharingViolation = 32;
        private const int ErrorLockViolation = 33;
        private const int ErrorUserMappedFile = 1224;
        private const int PathLockStripeCount = 64;

        private static readonly int[] CommitRetryDelaysMs = { 10, 20, 40, 80 };
        private static readonly object[] PathLocks = CreatePathLocks();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName,
            string newFileName, int flags);

        private static DateTime _lastDiskCheck = DateTime.MinValue;
        private static readonly TimeSpan DiskCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 定期检查磁盘空间；可用空间低于 200 MB 时记录警告。
        /// 检查频率限制为每 5 分钟一次，避免每次写入均访问磁盘状态。
        /// </summary>
        private static void CheckDiskSpace(string filePath)
        {
            try
            {
                var now = DateTime.Now;
                if (now - _lastDiskCheck < DiskCheckInterval) return;
                _lastDiskCheck = now;

                var root = Path.GetPathRoot(Path.GetFullPath(filePath));
                if (string.IsNullOrEmpty(root)) return;

                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.AvailableFreeSpace < 200 * 1024 * 1024)  // 200MB
                {
                    Logger.Warn($"[磁盘预警] 磁盘空间不足: {root} 剩余 {drive.AvailableFreeSpace / 1024 / 1024}MB");
                }
            }
            catch { /* 磁盘检查异常不得影响文件保存 */ }
        }

        public static string SaveBase64Image(string base64Str, string mimeType, string saveDir, string requestId, string prefix)
        {
            if (string.IsNullOrEmpty(base64Str)) return "";

            try
            {
                var ext = MimeTypeToExtension(mimeType);
                var filePath = PathHelper.ResolveExactSaveFile(saveDir, requestId, prefix, ext);
                CheckDiskSpace(filePath);
                var bytesWritten = WriteBase64ToFile(base64Str, filePath);
                Logger.Debug($"已保存图片: {filePath} ({bytesWritten} 字节)");
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error("保存Base64图片失败", ex);
                return "";
            }
        }

        public static string SaveBase64ImageToFile(string base64Str, string filePath)
        {
            if (string.IsNullOrEmpty(base64Str)) return "";

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                CheckDiskSpace(filePath);
                var bytesWritten = WriteBase64ToFile(base64Str, filePath);
                Logger.Debug($"已保存图片: {filePath} ({bytesWritten} 字节)");
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error($"保存图片失败 {filePath}", ex);
                return "";
            }
        }

        public static string SaveJsonFile(string jsonStr, string saveDir, string requestId, string fileName)
        {
            if (string.IsNullOrEmpty(jsonStr)) return "";

            try
            {
                var dir = PathHelper.EnsureRequestFolder(saveDir, requestId);
                var filePath = Path.Combine(dir, fileName);
                CheckDiskSpace(filePath);
                File.WriteAllText(filePath, jsonStr, System.Text.Encoding.UTF8);
                Logger.Debug($"已保存JSON: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error("保存JSON文件失败", ex);
                return "";
            }
        }

        private static string MimeTypeToExtension(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return ".dat";
            mimeType = mimeType.ToLower();
            if (mimeType.Contains("jpeg") || mimeType.Contains("jpg")) return ".jpg";
            if (mimeType.Contains("png")) return ".png";
            if (mimeType.Contains("bmp")) return ".bmp";
            if (mimeType.Contains("gif")) return ".gif";
            if (mimeType.Contains("tiff")) return ".tiff";
            return ".dat";
        }

        /// <summary>
        /// 使用固定大小缓冲区直接解码到磁盘，避免在托管进程的大对象堆上再次分配与图像等大的 byte[]。
        /// 先写入临时文件，防止无效 Base64 数据破坏已有文件。
        /// </summary>
        private static long WriteBase64ToFile(string base64Str, string filePath)
        {
            lock (GetPathLock(filePath))
            {
                var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    long length;
                    using (var output = new FileStream(tempPath, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None, 64 * 1024,
                        FileOptions.SequentialScan))
                    using (var transform = new FromBase64Transform(
                        FromBase64TransformMode.IgnoreWhiteSpaces))
                    using (var decoder = new CryptoStream(output, transform,
                        CryptoStreamMode.Write))
                    {
                        var inputBuffer = new byte[32 * 1024];
                        var offset = 0;
                        while (offset < base64Str.Length)
                        {
                            var charCount = Math.Min(inputBuffer.Length,
                                base64Str.Length - offset);
                            var byteCount = Encoding.ASCII.GetBytes(base64Str, offset,
                                charCount, inputBuffer, 0);
                            decoder.Write(inputBuffer, 0, byteCount);
                            offset += charCount;
                        }
                        decoder.FlushFinalBlock();
                        output.Flush(true);
                        length = output.Length;
                    }

                    CommitTempFile(tempPath, filePath);
                    return length;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 将 Base64 编码的 8 位灰度 raw 像素数据（无 BMP 头）解码并写入标准 BMP 文件。
        /// </summary>
        public static string SaveRawGrayscaleAsBmp(string base64Str, string saveDir,
            string requestId, int width, int height)
        {
            if (string.IsNullOrEmpty(base64Str)) return "";

            try
            {
                var filePath = PathHelper.ResolveExactSaveFile(saveDir, requestId,
                    "fingerprint_undistorted", ".bmp");
                return WriteBmpFile(base64Str, filePath, width, height);
            }
            catch (Exception ex)
            {
                Logger.Debug("保存无畸变BMP图片失败: " + ex.Message);
                return "";
            }
        }

        /// <summary>
        /// 将 Base64 编码的 8 位灰度 raw 像素数据解码并写入指定路径的 BMP 文件（直接覆盖）。
        /// </summary>
        public static string SaveRawGrayscaleAsBmpToFile(string base64Str, string filePath,
            int width, int height)
        {
            if (string.IsNullOrEmpty(base64Str)) return "";
            if (string.IsNullOrEmpty(filePath)) return "";

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return WriteBmpFile(base64Str, filePath, width, height);
            }
            catch (Exception ex)
            {
                Logger.Debug("保存无畸变BMP图片失败: " + ex.Message);
                return "";
            }
        }

        private static string WriteBmpFile(string base64Str, string filePath,
            int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            var expectedLen = checked(width * height);
            var rowSize = checked(((width + 3) / 4) * 4);
            var pixelDataSize = checked(rowSize * height);
            const int paletteSize = 256 * 4;
            const int headerSize = 14 + 40 + paletteSize;
            var fileSize = checked(headerSize + pixelDataSize);

            lock (GetPathLock(filePath))
            {
                // 原子写入：先写临时文件，再覆盖目标，避免第三方读到未写完的半截文件
                var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 64 * 1024, FileOptions.RandomAccess))
                    {
                        // 预先设置文件大小，使未写入的填充区和缺失源行保持补零，并与旧版实现一致。
                        fs.SetLength(fileSize);
                        fs.Position = 0;
                        using (var bw = new BinaryWriter(fs, Encoding.UTF8, true))
                        {
                            // BITMAPFILEHEADER（14 字节）
                            bw.Write((short)0x4D42);
                            bw.Write(fileSize);
                            bw.Write((short)0);
                            bw.Write((short)0);
                            bw.Write(headerSize);

                            // BITMAPINFOHEADER（40 字节）
                            bw.Write(40);
                            bw.Write(width);
                            bw.Write(height);
                            bw.Write((short)1);
                            bw.Write((short)8);
                            bw.Write(0);
                            bw.Write(pixelDataSize);
                            bw.Write(0);
                            bw.Write(0);
                            bw.Write(256);
                            bw.Write(0);

                            // 灰度调色板：256 个表项（B、G、R、0）
                            for (int i = 0; i < 256; i++)
                            {
                                bw.Write((byte)i);
                                bw.Write((byte)i);
                                bw.Write((byte)i);
                                bw.Write((byte)0);
                            }
                            bw.Flush();
                        }

                        long decodedLength;
                        var pixelWriter = new BottomUpBmpPixelStream(fs, headerSize,
                            width, height, rowSize);
                        using (var transform = new FromBase64Transform(
                            FromBase64TransformMode.IgnoreWhiteSpaces))
                        using (var decoder = new CryptoStream(pixelWriter, transform,
                            CryptoStreamMode.Write))
                        {
                            var inputBuffer = new byte[32 * 1024];
                            var offset = 0;
                            while (offset < base64Str.Length)
                            {
                                var charCount = Math.Min(inputBuffer.Length,
                                    base64Str.Length - offset);
                                var byteCount = Encoding.ASCII.GetBytes(base64Str, offset,
                                    charCount, inputBuffer, 0);
                                decoder.Write(inputBuffer, 0, byteCount);
                                offset += charCount;
                            }
                            decoder.FlushFinalBlock();
                            decodedLength = pixelWriter.TotalDecodedBytes;
                        }

                        if (decodedLength != expectedLen)
                        {
                            Logger.Warn($"[无畸变BMP] 像素数据长度异常: 期望{expectedLen}, 实际{decodedLength}");
                        }

                        fs.Flush(true);
                    }

                    CommitTempFile(tempPath, filePath);
                    Logger.Debug($"已保存无畸变BMP: {filePath}");
                    return filePath;
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                    catch { }
                }
            }
        }

        private static object[] CreatePathLocks()
        {
            var locks = new object[PathLockStripeCount];
            for (var i = 0; i < locks.Length; i++)
                locks[i] = new object();
            return locks;
        }

        private static object GetPathLock(string filePath)
        {
            var normalizedPath = Path.GetFullPath(filePath);
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(normalizedPath);
            return PathLocks[hash & (PathLockStripeCount - 1)];
        }

        /// <summary>
        /// 按源数据自顶向下的顺序接收解码后的原始像素，并将完整行写入 BMP 自底向上的对应位置。
        /// 内存中仅保留一行数据，避免在大对象堆分配整幅图像，同时保持既有正高度 BMP 的字节布局。
        /// </summary>
        private sealed class BottomUpBmpPixelStream : Stream
        {
            private readonly FileStream _output;
            private readonly long _pixelDataOffset;
            private readonly int _width;
            private readonly int _height;
            private readonly int _rowSize;
            private readonly byte[] _rowBuffer;
            private int _rowBufferCount;
            private int _sourceRow;
            private bool _completed;

            internal BottomUpBmpPixelStream(FileStream output, long pixelDataOffset,
                int width, int height, int rowSize)
            {
                _output = output ?? throw new ArgumentNullException(nameof(output));
                _pixelDataOffset = pixelDataOffset;
                _width = width;
                _height = height;
                _rowSize = rowSize;
                _rowBuffer = new byte[width];
            }

            internal long TotalDecodedBytes { get; private set; }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => TotalDecodedBytes;

            public override long Position
            {
                get => TotalDecodedBytes;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count)
                    throw new ArgumentOutOfRangeException();
                if (_completed) throw new ObjectDisposedException(nameof(BottomUpBmpPixelStream));

                TotalDecodedBytes += count;
                while (count > 0 && _sourceRow < _height)
                {
                    var copyLength = Math.Min(_width - _rowBufferCount, count);
                    Buffer.BlockCopy(buffer, offset, _rowBuffer, _rowBufferCount,
                        copyLength);
                    _rowBufferCount += copyLength;
                    offset += copyLength;
                    count -= copyLength;

                    if (_rowBufferCount == _width)
                        WriteCurrentRow();
                }
            }

            internal void Complete()
            {
                if (_completed) return;
                if (_rowBufferCount > 0 && _sourceRow < _height)
                    WriteCurrentRow();
                _completed = true;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Complete();
                base.Dispose(disposing);
            }

            private void WriteCurrentRow()
            {
                var destinationRow = _height - 1 - _sourceRow;
                _output.Position = _pixelDataOffset +
                    ((long)destinationRow * _rowSize);
                _output.Write(_rowBuffer, 0, _rowBufferCount);
                Array.Clear(_rowBuffer, 0, _rowBuffer.Length);
                _rowBufferCount = 0;
                _sourceRow++;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// 以原子方式发布已完整写入的临时文件。临时路径和目标路径位于同一目录，
        /// MoveFileEx 执行同卷重命名，读取方只能看到旧文件或新文件的完整版本。
        /// </summary>
        private static void CommitTempFile(string tempPath, string filePath)
        {
            for (var attempt = 0; ; attempt++)
            {
                if (MoveFileEx(tempPath, filePath,
                    MoveFileReplaceExisting | MoveFileWriteThrough))
                {
                    return;
                }

                var errorCode = Marshal.GetLastWin32Error();
                if (!IsTransientCommitError(errorCode) ||
                    attempt >= CommitRetryDelaysMs.Length)
                {
                    throw new Win32Exception(errorCode,
                        "原子替换文件失败: " + filePath);
                }

                var delayMs = CommitRetryDelaysMs[attempt];
                Logger.Debug($"[文件保存] 原子替换暂时失败，{delayMs}ms 后重试：" +
                    $"错误码={errorCode}，尝试次数={attempt + 1}，路径={filePath}");
                Thread.Sleep(delayMs);
            }
        }

        private static bool IsTransientCommitError(int errorCode)
        {
            return errorCode == ErrorAccessDenied ||
                errorCode == ErrorSharingViolation ||
                errorCode == ErrorLockViolation ||
                errorCode == ErrorUserMappedFile;
        }
    }
}
