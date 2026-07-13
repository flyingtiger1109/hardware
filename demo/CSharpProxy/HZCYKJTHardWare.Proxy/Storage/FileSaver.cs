using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Storage
{
    public static class FileSaver
    {
        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileWriteThrough = 0x8;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName,
            string newFileName, int flags);

        private static DateTime _lastDiskCheck = DateTime.MinValue;
        private static readonly TimeSpan DiskCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Periodic disk space check. Warns if free space drops below 200MB.
        /// Avoids checking on every write — throttled to every 5 minutes.
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
            catch { /* Disk check must not throw */ }
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
        /// Decode directly to disk with a fixed-size buffer. This avoids a second
        /// image-sized byte[] allocation on the LOH in the x86 process.
        /// A temporary file prevents invalid Base64 from damaging an existing file.
        /// </summary>
        private static long WriteBase64ToFile(string base64Str, string filePath)
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
            var decoded = Convert.FromBase64String(base64Str);
            var expectedLen = width * height;
            if (decoded.Length != expectedLen)
            {
                Logger.Warn($"[无畸变BMP] 像素数据长度异常: 期望{expectedLen}, 实际{decoded.Length}");
            }

            // 原子写入：先写临时文件，再覆盖目标，避免第三方读到未写完的半截文件
            var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                using (var bw = new BinaryWriter(fs))
                {
                    int rowSize = ((width * 8 + 31) / 32) * 4;
                    int pixelDataSize = rowSize * height;
                    int paletteSize = 256 * 4;
                    int headerSize = 14 + 40 + paletteSize;
                    int fileSize = headerSize + pixelDataSize;

                    // BITMAPFILEHEADER (14 bytes)
                    bw.Write((short)0x4D42);
                    bw.Write(fileSize);
                    bw.Write((short)0);
                    bw.Write((short)0);
                    bw.Write(headerSize);

                    // BITMAPINFOHEADER (40 bytes)
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

                    // Grayscale palette: 256 entries (B, G, R, 0)
                    for (int i = 0; i < 256; i++)
                    {
                        bw.Write((byte)i);
                        bw.Write((byte)i);
                        bw.Write((byte)i);
                        bw.Write((byte)0);
                    }

                    // Pixel data (bottom-up)
                    for (int y = height - 1; y >= 0; y--)
                    {
                        int srcOffset = y * width;
                        int copyLen = Math.Min(width, decoded.Length - srcOffset);
                        if (copyLen > 0)
                            bw.Write(decoded, srcOffset, copyLen);
                        for (int p = copyLen; p < rowSize; p++)
                            bw.Write((byte)0);
                    }

                    bw.Flush();
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

        /// <summary>
        /// Atomically publishes a fully-written temporary file. Both paths are
        /// created in the same directory, so MoveFileEx performs a same-volume
        /// rename and the reader observes either the previous or the new file.
        /// </summary>
        private static void CommitTempFile(string tempPath, string filePath)
        {
            if (MoveFileEx(tempPath, filePath,
                MoveFileReplaceExisting | MoveFileWriteThrough))
            {
                return;
            }

            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "原子替换文件失败: " + filePath);
        }
    }
}
