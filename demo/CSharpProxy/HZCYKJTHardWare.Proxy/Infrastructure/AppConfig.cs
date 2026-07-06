using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public sealed class PlatePreviewCameraConfig
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = "";
        public int Port { get; set; } = 554;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public int StreamChannel { get; set; } = 101;
    }

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
        public string SubnetPrefix { get; set; } = "192.168.20";

        // DLL callback server (where to send results back to DLL)
        public string DllCallbackHost { get; set; } = "";
        public int DllCallbackPort { get; set; } = 39091;
        public string DllCallbackBasePath { get; set; } = "/HZCYKJTHardWare/callback";

        // Preview settings
        public int RtspNetworkCachingMs { get; set; } = 50;
        public int RtspLiveCachingMs { get; set; } = 50;
        public int PreviewCheckHwndIntervalMs { get; set; } = 500;
        public string RtspTransport { get; set; } = "tcp";   // ""=auto, "tcp" 强制TCP(需live555)
        public PlatePreviewCameraConfig PlatePreviewCJ { get; set; } = new PlatePreviewCameraConfig();
        public PlatePreviewCameraConfig PlatePreviewRJ2 { get; set; } = new PlatePreviewCameraConfig();
        public PlatePreviewCameraConfig PlatePreviewRJ3 { get; set; } = new PlatePreviewCameraConfig();
        // Save settings
        public string DefaultSaveDir { get; set; } = @".\captures";
        public bool CreateDateFolder { get; set; } = true;
        public bool CreateRequestFolder { get; set; } = true;

        // Paths
        public string ExeDir { get; set; }
        public string VlcDir { get; set; }

        // Log settings
        public string LogLevel { get; set; } = "info";

        /// <summary>
        /// Unified config file shared by the DLL and C# proxy.
        /// </summary>
        private const string ConfigFile = "HZCYKJTHardWare.json";

        private static AppConfig Load()
        {
            var config = new AppConfig();
            config.ExeDir = AppDomain.CurrentDomain.BaseDirectory;

            var jsonPath = Path.Combine(config.ExeDir, ConfigFile);
            if (!File.Exists(jsonPath))
            {
                Logger.Warn($"Config file not found: {ConfigFile}, using defaults");
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
                    config.PreviewCheckHwndIntervalMs = Math.Max(100,
                        preview.Value<int?>("check_hwnd_interval_ms") ?? config.PreviewCheckHwndIntervalMs);

                    var plate = preview["plate"];
                    if (plate != null)
                    {
                        // Flat camera configuration only. Direction-to-camera composition is
                        // intentionally owned by the third-party caller.
                        config.PlatePreviewCJ = ReadPlatePreviewCamera(plate["cj"]);
                        config.PlatePreviewRJ2 = ReadPlatePreviewCamera(plate["rj2"]);
                        config.PlatePreviewRJ3 = ReadPlatePreviewCamera(plate["rj3"]);
                    }
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
                {
                    config.LogLevel = log.Value<string>("level") ?? config.LogLevel;
                }

                Logger.SetMinLevel(config.LogLevel);
                Logger.Info($"配置文件已加载: {jsonPath}");
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

        private static PlatePreviewCameraConfig ReadPlatePreviewCamera(JToken token)
        {
            var camera = new PlatePreviewCameraConfig();
            if (token == null)
                return camera;

            camera.Enabled = token.Value<bool?>("enabled") ?? false;
            camera.Host = token.Value<string>("host") ?? "";
            camera.Port = token.Value<int?>("port") ?? 554;
            camera.Username = token.Value<string>("username") ?? "";
            camera.Password = token.Value<string>("password") ?? "";
            camera.StreamChannel = token.Value<int?>("stream_channel") ?? 101;
            if (camera.Port <= 0 || camera.Port > 65535)
                camera.Port = 554;
            if (camera.StreamChannel != 101 && camera.StreamChannel != 102)
                camera.StreamChannel = 101;
            return camera;
        }

        public PlatePreviewCameraConfig GetPlatePreviewCamera(string plateCode)
        {
            if (string.Equals(plateCode, "rj2", StringComparison.OrdinalIgnoreCase))
                return PlatePreviewRJ2;
            if (string.Equals(plateCode, "rj3", StringComparison.OrdinalIgnoreCase))
                return PlatePreviewRJ3;
            if (string.Equals(plateCode, "cj", StringComparison.OrdinalIgnoreCase))
                return PlatePreviewCJ;
            return null;
        }

        public string GetPlatePreviewUrl(string plateCode)
        {
            var camera = GetPlatePreviewCamera(plateCode);
            if (camera == null || !camera.Enabled || string.IsNullOrWhiteSpace(camera.Host))
                return "";

            var host = camera.Host.Trim();
            if (host.IndexOf(':') >= 0 && !host.StartsWith("[", StringComparison.Ordinal))
                host = "[" + host + "]";

            var authority = "";
            if (!string.IsNullOrEmpty(camera.Username) || !string.IsNullOrEmpty(camera.Password))
            {
                authority = Uri.EscapeDataString(camera.Username ?? "");
                if (!string.IsNullOrEmpty(camera.Password))
                    authority += ":" + Uri.EscapeDataString(camera.Password);
                authority += "@";
            }

            return $"rtsp://{authority}{host}:{camera.Port}/Streaming/Channels/{camera.StreamChannel}";
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
