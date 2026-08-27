using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        public DeviceMode DeviceMode { get; set; } = DeviceMode.Full;
        public string DeviceModeName { get; set; } = "完整设备模式";

        // DLL 通信服务
        public string DllServerHost { get; set; } = "127.0.0.1";
        public int DllServerPort { get; set; } = 18080;

        // 终端回调接收服务
        public string CallbackListenHost { get; set; } = "0.0.0.0";
        public string CallbackPublicHost { get; set; } = "";  // public_host: terminal callback address for terminals to reach back
        public int CallbackListenPort { get; set; } = 18081;
        public string CallbackPath { get; set; } = "/terminal-callback";

        // 终端配置
        public string TerminalScheme { get; set; } = "http";
        public int TerminalPort { get; set; } = 9098;
        public int Terminal1HostSuffix { get; set; } = 30;
        public int Terminal2HostSuffix { get; set; } = 31;
        public string Terminal1Name { get; set; } = "左通道";
        public string Terminal2Name { get; set; } = "右通道";
        // 未提供 devices/auto_subnet_devices 时保持旧版默认双终端行为；
        // 一旦提供设备列表，则只有列表中实际出现的方向视为已配置。
        public bool Terminal1Configured { get; private set; } = true;
        public bool Terminal2Configured { get; private set; } = true;
        public int DefaultTerminalIndex { get; set; } = 1;
        public string SubnetPrefix { get; set; } = "192.168.20";

        // DLL 回调服务（用于向 DLL 返回结果）
        public string DllCallbackHost { get; set; } = "";
        public int DllCallbackPort { get; set; } = 39091;
        public string DllCallbackBasePath { get; set; } = "/HZCYKJTHardWare/callback";

        // 预览配置
        public int RtspNetworkCachingMs { get; set; } = 50;
        public int RtspLiveCachingMs { get; set; } = 50;
        public int PreviewCheckHwndIntervalMs { get; set; } = 500;
        public string RtspTransport { get; set; } = "tcp";   // ""=auto, "tcp" 强制TCP(需live555)
        public PlatePreviewCameraConfig PlatePreviewCJ { get; set; } = new PlatePreviewCameraConfig();
        public PlatePreviewCameraConfig PlatePreviewRJ2 { get; set; } = new PlatePreviewCameraConfig();
        public PlatePreviewCameraConfig PlatePreviewRJ3 { get; set; } = new PlatePreviewCameraConfig();
        // 保存配置
        public string DefaultSaveDir { get; set; } = @".\captures";
        public bool CreateDateFolder { get; set; } = true;
        public bool CreateRequestFolder { get; set; } = true;

        // 路径配置
        public string ExeDir { get; set; }
        public string VlcDir { get; set; }

        // 日志配置
        public string LogLevel { get; set; } = "info";
        public int LogRetentionDays { get; set; } = 30;
        public int LogMaxTotalSizeMb { get; set; } = 2048;
        public int LogDiskWarningFreeMb { get; set; } = 2048;
        public int LogFlushIntervalMs { get; set; } = 500;
        public int LogFlushBatchSize { get; set; } = 50;

        /// <summary>
        /// DLL 与 C# Proxy 共用的统一配置文件。
        /// </summary>
        private const string ConfigFile = "HZCYKJTHardWare.json";

        // 兼容现场配置中把数值型 host_suffix 留空的写法：
        //   "host_suffix":
        // 在正式 JSON 解析前归一化为 null，按未配置处理，避免整份配置回退到默认 IP。
        private static readonly Regex EmptyHostSuffixRegex = new Regex(
            "(\\\"host_suffix\\\"\\s*:\\s*)(?=[,}])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static AppConfig Load()
        {
            var config = new AppConfig();
            config.ExeDir = AppDomain.CurrentDomain.BaseDirectory;

            var jsonPath = Path.Combine(config.ExeDir, ConfigFile);
            if (!File.Exists(jsonPath))
            {
                Logger.Warn("DeviceMode配置项所在配置文件不存在，回退到DeviceMode=1");
                Logger.Warn($"未找到配置文件：{ConfigFile}，将使用默认配置");
                return config;
            }

            try
            {
                var json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var normalizedJson = NormalizeEmptyHostSuffix(json);
                if (!string.Equals(json, normalizedJson, StringComparison.Ordinal))
                {
                    Logger.Info("[终端配置] 检测到空 host_suffix，已按 null 归一化并视为未配置");
                    json = normalizedJson;
                }
                var obj = JObject.Parse(json);

                config.DeviceMode = ResolveDeviceMode(obj["device_mode"],
                    message => Logger.Warn(message));
                config.DeviceModeName = ResolveDeviceModeName(
                    obj["device_mode_names"], config.DeviceMode);

                // 同时兼容旧版 C# 配置键和统一 DLL 配置键
                var dllServer = obj["dll_server"] ?? obj["delphi_server"];
                if (dllServer != null)
                {
                    config.DllServerHost = dllServer.Value<string>("host") ?? config.DllServerHost;
                    config.DllServerPort = dllServer.Value<int?>("port") ?? config.DllServerPort;
                }

                // terminal_callback_server 配置段
                var callbackServer = obj["terminal_callback_server"];
                if (callbackServer != null)
                {
                    config.CallbackListenHost = callbackServer.Value<string>("listen_host") ?? config.CallbackListenHost;
                    config.CallbackPublicHost = callbackServer.Value<string>("public_host") ?? config.CallbackPublicHost;
                    config.CallbackListenPort = callbackServer.Value<int?>("port") ?? config.CallbackListenPort;
                    config.CallbackPath = callbackServer.Value<string>("path") ?? config.CallbackPath;
                }

                // terminal 配置段
                var terminal = obj["terminal"];
                if (terminal != null)
                {
                    config.TerminalScheme = terminal.Value<string>("scheme") ?? config.TerminalScheme;
                    config.TerminalPort = terminal.Value<int?>("port") ?? config.TerminalPort;
                    config.DefaultTerminalIndex = ResolveTerminalIndex(
                        terminal.Value<int?>("default_index"),
                        message => Logger.Warn(message));
                    config.SubnetPrefix = terminal.Value<string>("subnet_prefix")
                        ?? terminal.Value<string>("preferred_subnet_prefix")
                        ?? config.SubnetPrefix;

                    // C# 配置键为 "devices"，同时兼容旧版 "auto_subnet_devices"
                    var devices = terminal["devices"] ?? terminal["auto_subnet_devices"];
                    if (devices != null)
                    {
                        config.Terminal1Configured = ResolveTerminalConfigured(devices, 1);
                        config.Terminal2Configured = ResolveTerminalConfigured(devices, 2);
                        foreach (var dev in devices)
                        {
                            var index = dev.Value<int?>("index") ?? 0;
                            var suffix = ResolveHostSuffix(dev);
                            var name = dev.Value<string>("name");
                            if (index == 1)
                            {
                                config.Terminal1HostSuffix = suffix;
                                if (!string.IsNullOrWhiteSpace(name))
                                    config.Terminal1Name = name.Trim();
                            }
                            if (index == 2)
                            {
                                config.Terminal2HostSuffix = suffix;
                                if (!string.IsNullOrWhiteSpace(name))
                                    config.Terminal2Name = name.Trim();
                            }
                        }
                    }
                }

                // callback_server 配置段
                var dllCallback = obj["callback_server"];
                if (dllCallback != null)
                {
                    config.DllCallbackHost = dllCallback.Value<string>("host") ?? config.DllCallbackHost;
                    config.DllCallbackPort = dllCallback.Value<int?>("port") ?? config.DllCallbackPort;
                    config.DllCallbackBasePath = dllCallback.Value<string>("base_path") ?? config.DllCallbackBasePath;
                }

                // preview 配置段
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
                        // 此处仅保存扁平化相机配置；方向与相机的组合关系由第三方调用方维护。
                        config.PlatePreviewCJ = ReadPlatePreviewCamera(plate["cj"]);
                        config.PlatePreviewRJ2 = ReadPlatePreviewCamera(plate["rj2"]);
                        config.PlatePreviewRJ3 = ReadPlatePreviewCamera(plate["rj3"]);
                    }
                }

                // 同时兼容旧版 C# 配置键和统一 DLL 配置键
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
                    config.LogRetentionDays = Math.Max(1, Math.Min(3650,
                        log.Value<int?>("retention_days") ?? config.LogRetentionDays));
                    config.LogMaxTotalSizeMb = Math.Max(16, Math.Min(102400,
                        log.Value<int?>("max_total_size_mb") ?? config.LogMaxTotalSizeMb));
                    config.LogDiskWarningFreeMb = Math.Max(0, Math.Min(102400,
                        log.Value<int?>("disk_warning_free_mb") ?? config.LogDiskWarningFreeMb));
                    config.LogFlushIntervalMs = Math.Max(50, Math.Min(10000,
                        log.Value<int?>("flush_interval_ms") ?? config.LogFlushIntervalMs));
                    config.LogFlushBatchSize = Math.Max(1, Math.Min(10000,
                        log.Value<int?>("flush_batch_size") ?? config.LogFlushBatchSize));
                }

                Logger.Configure(config.LogRetentionDays,
                    config.LogMaxTotalSizeMb, config.LogDiskWarningFreeMb,
                    config.LogFlushIntervalMs, config.LogFlushBatchSize);
                Logger.SetMinLevel(config.LogLevel);
                LogTerminalConfiguration(config);
                Logger.Info($"配置文件已加载：{jsonPath}");
            }
            catch (Exception ex)
            {
                config.DeviceMode = DeviceMode.Full;
                Logger.Error($"加载配置失败：{ex.Message}；回退到 DeviceMode=1");
            }

            return config;
        }

        /// <summary>
        /// 兼容终端 host_suffix 冒号后直接为空的现场配置。
        /// 仅处理紧邻逗号或右大括号的空值，不放宽其他 JSON 错误。
        /// </summary>
        internal static string NormalizeEmptyHostSuffix(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;
            return EmptyHostSuffixRegex.Replace(json, "$1null");
        }

        private static void LogTerminalConfiguration(AppConfig config)
        {
            var summary = $"[终端配置] 终端1={(config.Terminal1Configured ? "已配置" : "未配置")}" +
                $"（名称={config.Terminal1Name}，主机后缀={config.Terminal1HostSuffix}），" +
                $"终端2={(config.Terminal2Configured ? "已配置" : "未配置")}" +
                $"（名称={config.Terminal2Name}，主机后缀={config.Terminal2HostSuffix}），" +
                $"默认终端={config.DefaultTerminalIndex}";
            Logger.Info(summary);

            if (config.Terminal1Configured != config.Terminal2Configured)
            {
                Logger.Info("[终端配置] 当前仅配置了一个方向；切换到未配置方向时将拒绝切换并返回 terminal_not_configured");
            }
        }

        internal static DeviceMode ResolveDeviceMode(JToken token, Action<string> warn)
        {
            if (token == null)
            {
                warn?.Invoke("DeviceMode缺失，回退到DeviceMode=1");
                return DeviceMode.Full;
            }

            if (token.Type == JTokenType.Integer)
            {
                var value = token.Value<int>();
                if (value == (int)DeviceMode.Full ||
                    value == (int)DeviceMode.RjCameraOnly)
                    return (DeviceMode)value;
            }

            warn?.Invoke("DeviceMode无效（仅支持1/2），回退到DeviceMode=1");
            return DeviceMode.Full;
        }

        internal static string ResolveDeviceModeName(JToken configuredNames,
            DeviceMode mode)
        {
            var configuredName = configuredNames?
                .Value<string>(((int)mode).ToString());
            if (!string.IsNullOrWhiteSpace(configuredName))
                return configuredName.Trim();
            return mode == DeviceMode.RjCameraOnly
                ? "RJ2/RJ3 镜头模式"
                : "完整设备模式";
        }

        internal static int ResolveTerminalIndex(int? configuredIndex,
            Action<string> warn)
        {
            if (!configuredIndex.HasValue)
                return 1;
            if (configuredIndex.Value == 1 || configuredIndex.Value == 2)
                return configuredIndex.Value;
            warn?.Invoke("terminal.default_index 配置非法，仅支持 1/2，回退终端 1");
            return 1;
        }

        /// <summary>
        /// 读取自动子网模式下的终端 IP 最后一段。
        /// 空字符串、缺失值、0 或超出 IPv4 最后一段范围的值均视为无效。
        /// </summary>
        internal static int ResolveHostSuffix(JToken device)
        {
            if (device == null)
                return 0;

            var token = device["host_suffix"];
            if (token == null || token.Type == JTokenType.Null)
                return 0;

            int suffix;
            if (!int.TryParse(token.ToString(), out suffix))
                return 0;

            return suffix >= 1 && suffix <= 254 ? suffix : 0;
        }

        /// <summary>
        /// 判断终端设备列表中是否存在指定方向且配置了有效 IP。
        /// 未提供设备列表时返回 true，以保持旧配置的默认双终端行为。
        /// </summary>
        internal static bool ResolveTerminalConfigured(JToken devices,
            int terminalIndex)
        {
            if (terminalIndex != 1 && terminalIndex != 2)
                return false;
            if (devices == null)
                return true;

            foreach (var dev in devices)
            {
                var index = dev.Value<int?>("index") ?? 0;
                if (index == terminalIndex && ResolveHostSuffix(dev) > 0)
                    return true;
            }
            return false;
        }

        public string GetTerminalName(int terminalIndex)
        {
            return terminalIndex == 2 ? Terminal2Name : Terminal1Name;
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
        /// 获取终端可访问的回调基础 URL。已配置 public_host 时优先使用，否则回退到局域网 IP。
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
