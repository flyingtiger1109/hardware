using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public class AppConfig
    {
        private static readonly Lazy<AppConfig> _lazy = new Lazy<AppConfig>(() => Load());
        public static AppConfig Instance => _lazy.Value;

        // DLL communication server
        public string DllServerHost { get; set; } = "127.0.0.1";
        public int DllServerPort { get; set; } = 18080;

        // Terminal callback receiver
        public string CallbackListenHost { get; set; } = "0.0.0.0";
        public string CallbackPublicHost { get; set; } = "";  // public_host: terminal callback address for terminals to reach back
        public int CallbackListenPort { get; set; } = 18081;
        public string CallbackPath { get; set; } = "/terminal-callback";

        // Terminal configuration
        public string TerminalScheme { get; set; } = "http";
        public int TerminalPort { get; set; } = 9098;
        public int Terminal1HostSuffix { get; set; } = 30;
        public int Terminal2HostSuffix { get; set; } = 31;
        public string Terminal1Name { get; set; } = "左通道";
        public string Terminal2Name { get; set; } = "右通道";
        public string SubnetPrefix { get; set; } = "192.168.20";

        // DLL callback server (where to send results back to DLL)
        public string DllCallbackHost { get; set; } = "";
        public int DllCallbackPort { get; set; } = 39091;
        public string DllCallbackBasePath { get; set; } = "/HZCYKJTHardWare/callback";

        // Preview settings
        public int RtspNetworkCachingMs { get; set; } = 50;
        public int RtspLiveCachingMs { get; set; } = 50;
        public string RtspTransport { get; set; } = "tcp";   // ""=auto, "tcp" 强制TCP(需live555)
        // Save settings
        public string DefaultSaveDir { get; set; } = @".\captures";
        public bool CreateDateFolder { get; set; } = true;
        public bool CreateRequestFolder { get; set; } = true;

        // Log settings. "info" is the production default; "debug" enables performance diagnostics.
        public string LogLevel { get; set; } = "info";

        // Paths
        public string ExeDir { get; set; }
        public string VlcDir { get; set; }

        /// <summary>
        /// Unified config file shared by the DLL and C# proxy.
        /// </summary>
        private const string ConfigFile = "HZCYKJTHardWare.json";
        private const string ConfigPathEnvironmentVariable = "HZCYKJTHARDWARE_CONFIG";

        private static AppConfig Load()
        {
            var config = new AppConfig();
            config.ExeDir = AppDomain.CurrentDomain.BaseDirectory;

            var jsonPath = ResolveConfigPath(config.ExeDir);
            if (!File.Exists(jsonPath))
            {
                Logger.Warn($"未找到配置文件：{ConfigFile}，使用默认配置");
                return config;
            }

            try
            {
                var json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var obj = JObject.Parse(json);

                // Supports both the old C# key and the unified DLL key.
                var dllServer = obj["dll_server"] ?? obj["delphi_server"];
                if (dllServer != null)
                {
                    config.DllServerHost = dllServer.Value<string>("host") ?? config.DllServerHost;
                    config.DllServerPort = dllServer.Value<int?>("port") ?? config.DllServerPort;
                }

                // terminal_callback_server
                var callbackServer = obj["terminal_callback_server"];
                if (callbackServer != null)
                {
                    config.CallbackListenHost = callbackServer.Value<string>("listen_host") ?? config.CallbackListenHost;
                    config.CallbackPublicHost = callbackServer.Value<string>("public_host") ?? config.CallbackPublicHost;
                    config.CallbackListenPort = callbackServer.Value<int?>("port") ?? config.CallbackListenPort;
                    config.CallbackPath = callbackServer.Value<string>("path") ?? config.CallbackPath;
                }

                // terminal
                var terminal = obj["terminal"];
                if (terminal != null)
                {
                    config.TerminalScheme = terminal.Value<string>("scheme") ?? config.TerminalScheme;
                    config.TerminalPort = terminal.Value<int?>("port") ?? config.TerminalPort;
                    config.SubnetPrefix = terminal.Value<string>("subnet_prefix")
                        ?? terminal.Value<string>("preferred_subnet_prefix")
                        ?? config.SubnetPrefix;

                    // C# key: "devices"; also supports legacy "auto_subnet_devices" and fixed terminal names.
                    var devices = terminal["devices"] ?? terminal["auto_subnet_devices"] ?? terminal["fixed_terminals"];
                    if (devices != null)
                    {
                        foreach (var dev in devices)
                        {
                            var index = dev.Value<int?>("index") ?? 0;
                            var name = dev.Value<string>("name");
                            var suffix = dev.Value<int?>("host_suffix");
                            if (index == 1)
                            {
                                if (suffix.HasValue) config.Terminal1HostSuffix = suffix.Value;
                                if (!string.IsNullOrWhiteSpace(name)) config.Terminal1Name = name;
                            }
                            if (index == 2)
                            {
                                if (suffix.HasValue) config.Terminal2HostSuffix = suffix.Value;
                                if (!string.IsNullOrWhiteSpace(name)) config.Terminal2Name = name;
                            }
                        }
                    }
                }

                // callback_server
                var dllCallback = obj["callback_server"];
                if (dllCallback != null)
                {
                    config.DllCallbackHost = dllCallback.Value<string>("host") ?? config.DllCallbackHost;
                    config.DllCallbackPort = dllCallback.Value<int?>("port") ?? config.DllCallbackPort;
                    config.DllCallbackBasePath = dllCallback.Value<string>("base_path") ?? config.DllCallbackBasePath;
                }

                // preview
                var preview = obj["preview"];
                if (preview != null)
                {
                    config.RtspNetworkCachingMs = preview.Value<int?>("rtsp_network_caching_ms") ?? config.RtspNetworkCachingMs;
                    config.RtspLiveCachingMs = preview.Value<int?>("rtsp_live_caching_ms") ?? config.RtspLiveCachingMs;
                    config.RtspTransport = preview.Value<string>("rtsp_transport") ?? config.RtspTransport;
                }

                // Supports both the old C# key and the unified DLL key.
                var save = obj["save"];
                if (save != null)
                {
                    config.DefaultSaveDir = save.Value<string>("default_dir")
                        ?? save.Value<string>("default_root")
                        ?? config.DefaultSaveDir;
                    config.CreateDateFolder = save.Value<bool?>("create_date_folder") ?? config.CreateDateFolder;
                    config.CreateRequestFolder = save.Value<bool?>("create_request_folder") ?? config.CreateRequestFolder;
                }

                var log = obj["log"];
                if (log != null)
                    config.LogLevel = log.Value<string>("level") ?? config.LogLevel;

                Logger.SetDebugEnabled(string.Equals(config.LogLevel, "debug", StringComparison.OrdinalIgnoreCase));

                Logger.Info($"配置加载完成：{jsonPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"加载配置失败：{ex.Message}");
            }

            return config;
        }

        private static string ResolveConfigPath(string exeDir)
        {
            var configuredPath = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    return Path.GetFullPath(configuredPath);
                }
                catch
                {
                    // The missing-file warning below includes the configured path.
                    return configuredPath;
                }
            }

            return Path.Combine(exeDir, ConfigFile);
        }

        public string GetDllServerUrl()
        {
            return $"http://{DllServerHost}:{DllServerPort}";
        }

        public string GetDllCallbackBaseUrl()
        {
            var host = string.IsNullOrEmpty(DllCallbackHost) ? "127.0.0.1" : DllCallbackHost;
            return $"http://{host}:{DllCallbackPort}{DllCallbackBasePath}";
        }

        /// <summary>
        /// Gets the terminal callback base URL that terminals can reach back to.
        /// Uses public_host if set, otherwise falls back to LAN IP.
        /// </summary>
        public string GetTerminalCallbackBaseUrl(string lanIp)
        {
            var host = !string.IsNullOrEmpty(CallbackPublicHost) ? CallbackPublicHost
                     : !string.IsNullOrEmpty(lanIp) ? lanIp
                     : "127.0.0.1";
            return $"http://{host}:{CallbackListenPort}{CallbackPath}";
        }
    }
}
