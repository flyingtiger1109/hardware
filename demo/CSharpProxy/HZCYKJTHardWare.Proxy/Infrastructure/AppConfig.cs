using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public class AppConfig
    {
        private static AppConfig _instance;
        public static AppConfig Instance => _instance ?? (_instance = Load());

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

        // Paths
        public string ExeDir { get; set; }
        public string VlcDir { get; set; }

        /// <summary>
        /// Config file name. Load order: <exe_dir>\HZCYKJTHardWare.Proxy.json first,
        /// then fall back to HZCYKJTHardWare.json (Delphi compat).
        /// </summary>
        private const string PrimaryConfigFile = "HZCYKJTHardWare.Proxy.json";
        private const string FallbackConfigFile = "HZCYKJTHardWare.json";

        private static AppConfig Load()
        {
            var config = new AppConfig();
            config.ExeDir = AppDomain.CurrentDomain.BaseDirectory;

            // Try primary config first, fall back to Delphi-compatible config
            var jsonPath = Path.Combine(config.ExeDir, PrimaryConfigFile);
            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(config.ExeDir, FallbackConfigFile);
                if (!File.Exists(jsonPath))
                {
                    Logger.Warn($"Config file not found: {PrimaryConfigFile} or {FallbackConfigFile}, using defaults");
                    return config;
                }
                Logger.Info($"Using fallback config: {jsonPath}");
            }

            try
            {
                var json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var obj = JObject.Parse(json);

                // dll_server (C# specific section name; also supports legacy "delphi_server" key)
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
                    config.SubnetPrefix = terminal.Value<string>("subnet_prefix") ?? config.SubnetPrefix;

                    // C# key: "devices"; also supports legacy "auto_subnet_devices"
                    var devices = terminal["devices"] ?? terminal["auto_subnet_devices"];
                    if (devices != null)
                    {
                        foreach (var dev in devices)
                        {
                            var index = dev.Value<int?>("index") ?? 0;
                            var suffix = dev.Value<int?>("host_suffix") ?? 0;
                            if (index == 1) config.Terminal1HostSuffix = suffix;
                            if (index == 2) config.Terminal2HostSuffix = suffix;
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

                // save (C# specific section)
                var save = obj["save"];
                if (save != null)
                {
                    config.DefaultSaveDir = save.Value<string>("default_dir") ?? config.DefaultSaveDir;
                    config.CreateDateFolder = save.Value<bool?>("create_date_folder") ?? config.CreateDateFolder;
                    config.CreateRequestFolder = save.Value<bool?>("create_request_folder") ?? config.CreateRequestFolder;
                }

                Logger.Info($"Config loaded from {jsonPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load config: {ex.Message}");
            }

            return config;
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
