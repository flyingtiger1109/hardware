using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    /// <summary>
    /// Extracts embedded VLC libraries to a fixed temp directory at runtime.
    /// Uses a deterministic directory name (not GUID) so that:
    ///  - On normal exit: ProcessExit handler cleans up.
    ///  - On crash/kill: NEXT startup cleans up the orphaned directory before re-extracting.
    /// This prevents temp directory accumulation over time.
    /// </summary>
    public static class VlcResourceExtractor
    {
        private static string _extractedDir;
        private static readonly object _lock = new object();
        private const string VlcDirName = "HZCYKJTHardWare_VLC";

        /// <summary>
        /// Gets the directory where VLC files have been extracted.
        /// Returns null if extraction hasn't happened yet.
        /// </summary>
        public static string ExtractedDirectory => _extractedDir;

        /// <summary>
        /// Extracts all embedded resources with prefix "vlc." to the fixed temp directory.
        /// On first run, cleans up any orphaned directory from a previous crash.
        /// Returns the extraction directory path, or null on failure.
        /// Thread-safe: only extracts once.
        /// </summary>
        public static string EnsureExtracted()
        {
            if (_extractedDir != null && Directory.Exists(_extractedDir))
                return _extractedDir;

            lock (_lock)
            {
                if (_extractedDir != null && Directory.Exists(_extractedDir))
                    return _extractedDir;

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceNames = assembly.GetManifestResourceNames()
                        .Where(n => n.StartsWith("vlc.", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (resourceNames.Length == 0)
                    {
                        Logger.Warn("未找到内置VLC资源，将尝试使用外部VLC运行库。");
                        return null;
                    }

                    // Use deterministic directory path (not GUID) so crash cleanup works
                    var tempDir = Path.Combine(Path.GetTempPath(), VlcDirName);

                    // Clean up orphaned directory from previous crash/kill
                    if (Directory.Exists(tempDir))
                    {
                        try
                        {
                            Directory.Delete(tempDir, true);
                            Logger.Info($"已清理上次残留的VLC临时目录: {tempDir}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"清理VLC临时目录失败(可能被其他实例占用): {ex.Message}");
                        }
                    }

                    Directory.CreateDirectory(tempDir);

                    Logger.Info($"解压 {resourceNames.Length} 个VLC资源到 {tempDir}");

                    foreach (var resourceName in resourceNames)
                    {
                        var relativePath = ResourceNameToPath(resourceName);
                        var fullPath = Path.Combine(tempDir, relativePath);

                        var dir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        using (var stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream == null) continue;
                            using (var file = File.Create(fullPath))
                            {
                                stream.CopyTo(file);
                            }
                        }
                    }

                    _extractedDir = Path.Combine(tempDir, "vlc");
                    if (!Directory.Exists(_extractedDir))
                    {
                        // If no "vlc" subdirectory in resources, use tempDir directly
                        _extractedDir = tempDir;
                    }

                    Logger.Info($"VLC已解压到 {_extractedDir}");

                    // Register graceful cleanup — but also handle crash via startup cleanup above
                    AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup();

                    return _extractedDir;
                }
                catch (Exception ex)
                {
                    Logger.Error("VLC资源解压失败", ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Converts embedded resource name to file path.
        /// "vlc.libvlc.dll" -> "vlc\libvlc.dll"
        /// </summary>
        private static string ResourceNameToPath(string resourceName)
        {
            var parts = resourceName.Split('.');
            if (parts.Length < 3) return resourceName.Replace('.', Path.DirectorySeparatorChar);

            var ext = parts[parts.Length - 1];
            var fileName = parts[parts.Length - 2] + "." + ext;
            var dirParts = new string[parts.Length - 2];
            Array.Copy(parts, dirParts, parts.Length - 2);
            var dir = string.Join(Path.DirectorySeparatorChar.ToString(), dirParts);

            return Path.Combine(dir, fileName);
        }

        /// <summary>
        /// Cleans up the extracted temp directory on graceful shutdown.
        /// Crash/kill cleanup is handled by EnsureExtracted() on next startup.
        /// </summary>
        public static void Cleanup()
        {
            if (string.IsNullOrEmpty(_extractedDir)) return;

            try
            {
                var tempDir = Path.GetDirectoryName(_extractedDir);
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                    Logger.Info($"已清理VLC临时目录: {tempDir}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"清理VLC临时目录失败: {ex.Message}");
            }
            finally
            {
                _extractedDir = null;
            }
        }
    }
}
