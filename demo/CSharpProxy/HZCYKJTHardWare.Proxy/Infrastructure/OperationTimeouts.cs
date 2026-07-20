namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    /// <summary>
    /// 面向终端操作的内部超时时限。
    /// 终端超时应短于 Proxy 接收超时，Proxy 接收超时应短于 DLL 的 HTTP 等待时限。
    /// </summary>
    internal static class OperationTimeouts
    {
        internal const int FaceTerminalRequestMs = 3000;
        internal const int FingerprintTerminalRequestMs = 4000;
        internal const int AsyncTerminalRequestMs = 4000;
        internal const int AuthorizeTerminalRequestMs = 4000;
        internal const int ProcessStartTerminalRequestMs = 5000;
        internal const int ProcessEndTerminalRequestMs = 3000;

        internal const int CaptureProxyWaitMs = 4500;
        internal const int AsyncProxyWaitMs = 4500;
        internal const int AuthorizeProxyWaitMs = 4500;
    }
}
