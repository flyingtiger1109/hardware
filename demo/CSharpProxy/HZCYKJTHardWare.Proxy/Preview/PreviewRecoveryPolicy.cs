namespace HZCYKJTHardWare.Proxy.Preview
{
    /// <summary>
    /// 所有预览协议共用的恢复节奏和候选提交边界。
    /// 协议、播放器和 HWND 处理仍由 PreviewManager 的独立恢复链路负责。
    /// </summary>
    internal static class PreviewRecoveryPolicy
    {
        internal const int CandidateReadyTimeoutMs = 8000;
        internal const int MaxBackoffDelayMs = 15000;

        internal static int GetRecoveryDelayMs(int attempt)
        {
            if (attempt <= 1)
                return 1000;
            if (attempt == 2)
                return 2000;
            if (attempt == 3)
                return 5000;
            if (attempt == 4)
                return 10000;
            return MaxBackoffDelayMs;
        }

        internal static int NextAttempt(int attempt)
        {
            if (attempt >= int.MaxValue)
                return int.MaxValue;
            return attempt < 0 ? 1 : attempt + 1;
        }

        internal static bool IsRetryableFailure(MjpegFailureKind failureKind)
        {
            return failureKind != MjpegFailureKind.RenderTargetFailure;
        }

        internal static bool ShouldCommitCandidate(bool sameSession,
            long expectedGeneration, long actualGeneration, bool candidateReady)
        {
            return sameSession && expectedGeneration == actualGeneration && candidateReady;
        }

        internal static bool ShouldLogRecoverySuccess(bool successAlreadyLogged)
        {
            return !successAlreadyLogged;
        }
    }
}
