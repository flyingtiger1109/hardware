using System;
using System.Collections.Generic;
using System.Linq;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public enum DeviceMode { Full = 1, RjCameraOnly = 2 }

    public enum DeviceCapability
    {
        PlateCJ, PlateRJ2, PlateRJ3, Face, Fingerprint, Iris, OCR, NfcCard,
        Authorize, TerminalControl, ProcessControl, Gate, Light, LightStrip
    }

    public sealed class DeviceCapabilityManager
    {
        private sealed class ThrottleState
        {
            public DateTime LastLoggedUtc;
            public int Suppressed;
        }

        private static readonly Lazy<DeviceCapabilityManager> LazyInstance =
            new Lazy<DeviceCapabilityManager>(() => new DeviceCapabilityManager(
                AppConfig.Instance.DeviceMode));
        private readonly HashSet<DeviceCapability> _capabilities;
        private readonly Dictionary<string, ThrottleState> _warningStates =
            new Dictionary<string, ThrottleState>(StringComparer.OrdinalIgnoreCase);
        private readonly object _warningLock = new object();

        public static DeviceCapabilityManager Instance => LazyInstance.Value;
        public DeviceMode Mode { get; }

        internal DeviceCapabilityManager(DeviceMode mode)
        {
            Mode = mode == DeviceMode.RjCameraOnly ? mode : DeviceMode.Full;
            _capabilities = Mode == DeviceMode.RjCameraOnly
                ? new HashSet<DeviceCapability>
                {
                    DeviceCapability.PlateRJ2, DeviceCapability.PlateRJ3
                }
                : new HashSet<DeviceCapability>(
                    Enum.GetValues(typeof(DeviceCapability)).Cast<DeviceCapability>());
        }

        public bool IsSupported(DeviceCapability capability) =>
            _capabilities.Contains(capability);

        public string CapabilitiesText => string.Join(", ",
            _capabilities.OrderBy(c => c.ToString()).Select(c => c.ToString()));

        public string BuildNotSupportedResult(string interfaceName,
            DeviceCapability capability)
        {
            WarnUnsupported(interfaceName, capability);
            return "{\"error\":true,\"code\":\"not_supported\"," +
                   "\"device_mode\":" + (int)Mode + "," +
                   "\"capability\":\"" + capability + "\"}";
        }

        public void WarnUnsupported(string interfaceName, DeviceCapability capability)
        {
            var key = (interfaceName ?? "unknown") + "|" + capability;
            var now = DateTime.UtcNow;
            lock (_warningLock)
            {
                if (!_warningStates.TryGetValue(key, out var state))
                {
                    state = new ThrottleState();
                    _warningStates[key] = state;
                }
                if (state.LastLoggedUtc != default(DateTime) &&
                    now - state.LastLoggedUtc < TimeSpan.FromSeconds(60))
                {
                    state.Suppressed++;
                    return;
                }
                var suppressed = state.Suppressed;
                state.Suppressed = 0;
                state.LastLoggedUtc = now;
                Logger.Warn("[硬件检测] 接口=" + interfaceName +
                    "，DeviceMode=" + (int)Mode + "，能力=" + capability +
                    "，结果=NotSupported" +
                    (suppressed > 0 ? ", Suppressed: " + suppressed : ""));
            }
        }

        public bool TryGetRequiredCapability(string path, out DeviceCapability capability)
        {
            switch ((path ?? "").Split('?')[0].ToLowerInvariant())
            {
                case "/preview/plate/rj2/start": case "/preview/plate/rj2/stop":
                    capability = DeviceCapability.PlateRJ2; return true;
                case "/preview/plate/rj3/start": case "/preview/plate/rj3/stop":
                    capability = DeviceCapability.PlateRJ3; return true;
                case "/preview/plate/cj/start": case "/preview/plate/cj/stop":
                    capability = DeviceCapability.PlateCJ; return true;
                case "/capture/face": case "/preview/camera/start":
                case "/preview/camera/stop": case "/preview/camera/url":
                    capability = DeviceCapability.Face; return true;
                case "/capture/fingerprint": case "/preview/fingerprint/start":
                case "/preview/fingerprint/stop": case "/preview/fingerprint/url":
                    capability = DeviceCapability.Fingerprint; return true;
                case "/capture/iris": case "/preview/iris/start":
                case "/preview/iris/stop": case "/preview/iris/url":
                    capability = DeviceCapability.Iris; return true;
                case "/ocr": capability = DeviceCapability.OCR; return true;
                case "/nfc": capability = DeviceCapability.NfcCard; return true;
                case "/authorize": capability = DeviceCapability.Authorize; return true;
                case "/terminal/switch": capability = DeviceCapability.TerminalControl; return true;
                case "/process/start": case "/process/end":
                    capability = DeviceCapability.ProcessControl; return true;
                default: capability = default(DeviceCapability); return false;
            }
        }

        public bool TryGetPreviewCapability(string resourceType,
            out DeviceCapability capability)
        {
            switch ((resourceType ?? "").ToLowerInvariant())
            {
                case "plate_rj2": capability = DeviceCapability.PlateRJ2; return true;
                case "plate_rj3": capability = DeviceCapability.PlateRJ3; return true;
                case "plate_cj": capability = DeviceCapability.PlateCJ; return true;
                case "camera": capability = DeviceCapability.Face; return true;
                case "fingerprint": capability = DeviceCapability.Fingerprint; return true;
                case "iris": capability = DeviceCapability.Iris; return true;
                default: capability = default(DeviceCapability); return false;
            }
        }

    }
}
