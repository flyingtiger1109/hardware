using System;
using System.Net;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Terminal
{
    public sealed class TerminalRouteSnapshot
    {
        internal TerminalRouteSnapshot(int terminalIndex, string terminalName,
            string baseUrl, long routeEpoch)
        {
            TerminalIndex = terminalIndex;
            TerminalName = terminalName ?? "";
            BaseUrl = baseUrl ?? "";
            RouteEpoch = routeEpoch;
        }

        public int TerminalIndex { get; }
        public string TerminalName { get; }
        public string BaseUrl { get; }
        public long RouteEpoch { get; }
    }

    /// <summary>
    /// Immutable routing context captured at request admission time.  The route,
    /// generation and cancellation token always belong to the same terminal epoch.
    /// </summary>
    public sealed class TerminalRouteEpochSnapshot
    {
        internal TerminalRouteEpochSnapshot(TerminalRouteSnapshot route,
            int generation, CancellationToken cancellationToken)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Generation = generation;
            CancellationToken = cancellationToken;
        }

        public TerminalRouteSnapshot Route { get; }
        public int Generation { get; }
        public CancellationToken CancellationToken { get; }
        public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;
    }

    public class TerminalManager
    {
        private readonly string[] _terminalUrls = new string[2];
        private readonly string[] _terminalNames = { "左通道", "右通道" };
        private int _currentIndex = 1; // 1-based
        private long _routeEpoch;
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

        public TerminalRouteSnapshot CurrentRoute
        {
            get
            {
                lock (_lock)
                {
                    return new TerminalRouteSnapshot(_currentIndex,
                        _terminalNames[_currentIndex - 1],
                        _terminalUrls[_currentIndex - 1], _routeEpoch);
                }
            }
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
            lock (_lock)
            {
                _currentIndex = index;
                _routeEpoch++;
            }
            Logger.Info($"已切换到终端: {CurrentName}");
            return true;
        }

        public bool TryResolveTerminalIndex(IPAddress remoteAddress, out int terminalIndex)
        {
            terminalIndex = 0;
            if (remoteAddress == null) return false;
            if (remoteAddress.IsIPv4MappedToIPv6)
                remoteAddress = remoteAddress.MapToIPv4();
            var addressText = remoteAddress.ToString();
            lock (_lock)
            {
                for (var i = 0; i < _terminalUrls.Length; i++)
                {
                    if (Uri.TryCreate(_terminalUrls[i], UriKind.Absolute, out var uri) &&
                        string.Equals(uri.Host, addressText,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        terminalIndex = i + 1;
                        return true;
                    }
                }
            }
            return false;
        }

        public void UpdateFromConfig()
        {
            var cfg = AppConfig.Instance;
            lock (_lock)
            {
                _terminalUrls[0] = $"{cfg.TerminalScheme}://{cfg.SubnetPrefix}.{cfg.Terminal1HostSuffix}:{cfg.TerminalPort}";
                _terminalUrls[1] = $"{cfg.TerminalScheme}://{cfg.SubnetPrefix}.{cfg.Terminal2HostSuffix}:{cfg.TerminalPort}";
            }
        }
    }
}
