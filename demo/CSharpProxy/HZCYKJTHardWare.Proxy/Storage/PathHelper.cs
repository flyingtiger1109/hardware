using System;
using System.IO;

namespace HZCYKJTHardWare.Proxy.Storage
{
    public static class PathHelper
    {
        public static string SafeResolveSaveDir(string saveDir)
        {
            if (string.IsNullOrEmpty(saveDir))
                saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captures");

            if (!Path.IsPathRooted(saveDir))
                saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, saveDir);

            return Path.GetFullPath(saveDir);
        }

        /// <summary>
        /// Resolve a file path (make absolute, create parent dir). Used when saveDir is a full file name.
        /// </summary>
        public static string ResolveExactSaveFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return filePath;
            var resolved = filePath;
            if (!Path.IsPathRooted(resolved))
                resolved = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, resolved);
            resolved = Path.GetFullPath(resolved);
            var parentDir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);
            return resolved;
        }

        public static string ResolveExactSaveFile(string saveDir, string requestId, string prefix, string extension)
        {
            var dir = EnsureRequestFolder(saveDir, requestId);
            var fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{extension.TrimStart('.')}";
            return Path.Combine(dir, fileName);
        }

        public static string CreateDateFolder(string baseDir)
        {
            var dir = Path.Combine(baseDir, DateTime.Now.ToString("yyyyMMdd"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public static string CreateRequestFolder(string baseDir, string requestId)
        {
            var safeId = string.IsNullOrEmpty(requestId) ? "unknown" : requestId;
            // Sanitize requestId for use as directory name
            foreach (var c in Path.GetInvalidFileNameChars())
                safeId = safeId.Replace(c, '_');
            var dir = Path.Combine(baseDir, safeId);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public static string EnsureRequestFolder(string saveDir, string requestId)
        {
            var dir = saveDir;
            var cfg = Infrastructure.AppConfig.Instance;
            if (cfg.CreateDateFolder)
                dir = CreateDateFolder(dir);
            if (cfg.CreateRequestFolder)
                dir = CreateRequestFolder(dir, requestId);
            return dir;
        }

        public static string EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
