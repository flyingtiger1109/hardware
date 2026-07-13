namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    /// <summary>
    /// Internal timeout budget for terminal-facing operations.
    /// Keep the terminal timeout shorter than the Proxy acceptance timeout,
    /// which in turn stays shorter than the DLL HTTP wait budget.
    /// </summary>
    internal static class OperationTimeouts
    {
        internal const int FaceTerminalRequestMs = 3000;
        internal const int FingerprintTerminalRequestMs = 4000;
        internal const int AsyncTerminalRequestMs = 4000;
        internal const int AuthorizeTerminalRequestMs = 4000;
        internal const int ProcessStartTerminalRequestMs = 5000;

        internal const int CaptureProxyWaitMs = 4500;
        internal const int AsyncProxyWaitMs = 4500;
        internal const int AuthorizeProxyWaitMs = 4500;
    }
}
