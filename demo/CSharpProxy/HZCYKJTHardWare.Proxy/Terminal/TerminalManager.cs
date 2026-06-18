using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Terminal
{
    public class TerminalManager
    {
        private readonly string[] _terminalUrls = new string[2];
        private readonly string[] _terminalNames = new string[2];
        private int _currentIndex = 1; // 1-based
        private readonly object _lock = new object();

        // Process state tracking (same as Delphi TTerminalManager)
        // Thread-safe: accessed from DLL command handler and callback handler concurrently
        private string _processSaveDir = "";
        private bool _processActive;

        public TerminalManager()
        {
            var cfg = AppConfig.Instance;
            _terminalUrls[0] = $"{cfg.TerminalScheme}://{cfg.SubnetPrefix}.{cfg.Terminal1HostSuffix}:{cfg.TerminalPort}";
            _terminalUrls[1] = $"{cfg.TerminalScheme}://{cfg.SubnetPrefix}.{cfg.Terminal2HostSuffix}:{cfg.TerminalPort}";
            _terminalNames[0] = cfg.Terminal1Name;
            _terminalNames[1] = cfg.Terminal2Name;
        }

        public int CurrentIndex
        {
            get { lock (_lock) return _currentIndex; }
        }
        public string CurrentBaseUrl
        {
            get { lock (_lock) return _terminalUrls[_currentIndex - 1]; }
        }
        public string CurrentName
        {
            get { lock (_lock) return _terminalNames[_currentIndex - 1]; }
        }

        public string ProcessSaveDir
        {
            get { lock (_lock) return _processSaveDir; }
            set { lock (_lock) _processSaveDir = value ?? ""; }
        }

        public bool ProcessActive
        {
            get { lock (_lock) return _processActive; }
            set { lock (_lock) _processActive = value; }
        }

        public bool IsSameTerminal(int index)
        {
            lock (_lock) return _currentIndex == index;
        }

        public bool SwitchTo(int index)
        {
            if (index < 1 || index > 2) return false;
            lock (_lock) { _currentIndex = index; }
            Logger.Info($"已切换到 {CurrentName} ({CurrentBaseUrl})");
            return true;
        }

        public void UpdateFromConfig()
        {
            var cfg = AppConfig.Instance;
            _terminalUrls[0] = $"{cfg.TerminalScheme}://{cfg.SubnetPrefix}.{cfg.Terminal1HostSuffix}:{cfg.TerminalPort}";
            _terminalUrls[1] = $"{cfg.TerminalScheme}://{cfg.SubnetPrefix}.{cfg.Terminal2HostSuffix}:{cfg.TerminalPort}";
            _terminalNames[0] = cfg.Terminal1Name;
            _terminalNames[1] = cfg.Terminal2Name;
        }
    }
}
