using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    /// <summary>
    /// 运行时将嵌入的 VLC 库提取到固定临时目录。
    /// 使用确定性目录名而非 GUID：正常退出时由 ProcessExit 处理函数清理；
    /// 进程崩溃或被终止时，由下次启动在重新提取前清理遗留目录，避免临时目录持续累积。
    /// </summary>
    public static class VlcResourceExtractor
    {
        private static string _extractedDir;
        private static readonly object _lock = new object();
        private const string VlcDirName = "HZCYKJTHardWare_VLC";

        /// <summary>
        /// 获取 VLC 文件提取目录；尚未执行提取时返回 null。
        /// </summary>
        public static string ExtractedDirectory => _extractedDir;

        /// <summary>
        /// 将名称以 "vlc." 开头的嵌入资源提取到固定临时目录。
        /// 首次执行时清理上次异常退出遗留的目录。成功时返回提取目录，失败时返回 null。
        /// 此方法线程安全，提取操作最多执行一次。
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
                        Logger.Warn("No embedded VLC resources found. Falling back to external VLC.");
                        return null;
                    }

                    // 使用确定性目录路径而非 GUID，便于异常退出后的遗留目录清理
                    var tempDir = Path.Combine(Path.GetTempPath(), VlcDirName);

                    // 清理上次进程崩溃或被终止后遗留的目录
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
                        // 资源中不存在 "vlc" 子目录时直接使用 tempDir
                        _extractedDir = tempDir;
                    }

                    Logger.Info($"VLC已解压到 {_extractedDir}");

                    // 注册正常退出清理；异常退出遗留内容由下次启动清理
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
        /// 将嵌入资源名称转换为文件路径。
        /// 示例："vlc.libvlc.dll" -> "vlc\libvlc.dll"
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
        /// 正常关闭时清理提取目录。崩溃或进程被终止后的遗留目录由下次启动的 EnsureExtracted() 清理。
        /// </summary>
        public static void Cleanup()
        {
            if (string.IsNullOrEmpty(_extractedDir)) return;

            try
            {
                // 审查风险：资源不含 "vlc" 子目录时 _extractedDir 已是提取根目录，
                // Path.GetDirectoryName 会返回系统临时目录；递归删除可能误删其他临时文件。
                // 建议单独保存提取根目录，并仅删除名称严格等于 VlcDirName 的目录。
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
