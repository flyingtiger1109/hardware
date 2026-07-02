using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Storage
{
    public static class FileSaver
    {
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
                    length = output.Length;
                }

                File.Copy(tempPath, filePath, true);
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
}
