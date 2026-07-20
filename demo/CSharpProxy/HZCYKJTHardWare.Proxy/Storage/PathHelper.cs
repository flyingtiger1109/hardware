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
        /// 解析文件路径，包括转换为绝对路径并创建父目录；适用于 saveDir 为完整文件名的场景。
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
            // 清理 requestId 中不适用于目录名的字符
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
