using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class PreviewRecoveryPolicyTests
    {
        [TestMethod]
        public void GetRecoveryDelayMs_UsesBoundedBackoff()
        {
            Assert.AreEqual(1000, PreviewManager.GetRecoveryDelayMs(1));
            Assert.AreEqual(2000, PreviewManager.GetRecoveryDelayMs(2));
            Assert.AreEqual(5000, PreviewManager.GetRecoveryDelayMs(3));
            Assert.AreEqual(10000, PreviewManager.GetRecoveryDelayMs(4));
            Assert.AreEqual(10000, PreviewManager.GetRecoveryDelayMs(100));
        }

        [TestMethod]
        public void RecoverableFailureIsWarning()
        {
            Assert.AreEqual("警告", PreviewManager.GetMjpegRecoveryFailureLevel(1));
        }

        [TestMethod]
        public void FinalRecoveryFailureIsError()
        {
            Assert.AreEqual("错误", PreviewManager.GetMjpegRecoveryFailureLevel(5));
        }

        [TestMethod]
        public void RecoverySuccessContainsNoFalseError()
        {
            var message = Logger.FormatModuleMessage(LogModules.Preview, "信息",
                "指纹预览已恢复：" + Logger.FormatContextMessage(
                    "RecoverFingerprintPreview", requestId: "REQ-1",
                    result: "Success", durationMs: 6124));

            StringAssert.Contains(message, "指纹预览已恢复");
            StringAssert.Contains(message, "Result=Success");
            Assert.IsFalse(message.Contains("[错误]"));
            Assert.IsFalse(message.Contains("错误="));
        }

        [TestMethod]
        public void ShouldValidatePreviewUrl_SkipsHttpMjpegUrls()
        {
            Assert.IsFalse(PreviewManager.ShouldValidatePreviewUrl("http://192.168.20.30/live.mjpg"));
            Assert.IsFalse(PreviewManager.ShouldValidatePreviewUrl("HTTPS://terminal/live"));
            Assert.IsTrue(PreviewManager.ShouldValidatePreviewUrl("rtsp://192.168.20.30/live"));
        }

        [TestMethod]
        public void NonHttpPreviewUsesVlcPrimaryPolicyForPlateSessions()
        {
            Assert.IsFalse(PreviewManager.IsMjpegFallbackApplicable(
                "rtsp://192.168.20.30/live"));
            Assert.IsTrue(PreviewManager.IsMjpegFallbackApplicable(
                "https://terminal/live.mjpg"));

            Assert.IsTrue(PreviewManager.IsPrimaryVlcAllowedForNonHttpPreview(
                PreviewResourceType.PlateCJ, PreviewSessionType.External));
            Assert.IsTrue(PreviewManager.IsPrimaryVlcAllowedForNonHttpPreview(
                PreviewResourceType.PlateRJ2, PreviewSessionType.External));
            Assert.IsTrue(PreviewManager.IsPrimaryVlcAllowedForNonHttpPreview(
                PreviewResourceType.PlateRJ3, PreviewSessionType.External));
            Assert.IsTrue(PreviewManager.IsPrimaryVlcAllowedForNonHttpPreview(
                PreviewResourceType.PlateCJ, PreviewSessionType.Local));
            Assert.IsFalse(PreviewManager.IsPrimaryVlcAllowedForNonHttpPreview(
                PreviewResourceType.Camera, PreviewSessionType.External));
        }

        [TestMethod]
        public void VlcRecoveryRequiresRealVideoSignalBeforeSuccess()
        {
            Assert.IsFalse(PreviewManager.ShouldReportVlcRecoverySuccess(
                playerIsRunning: true, hasVideoRecoverySignal: false));
            Assert.IsFalse(PreviewManager.ShouldReportVlcRecoverySuccess(
                playerIsRunning: false, hasVideoRecoverySignal: true));
            Assert.IsTrue(PreviewManager.ShouldReportVlcRecoverySuccess(
                playerIsRunning: true, hasVideoRecoverySignal: true));
        }

        [TestMethod]
        public void RecoverySummaryKeepsOnlyProductionFields()
        {
            var started = PreviewManager.BuildPreviewRecoverySummary(
                "车牌CJ", "出现视频流中断，正在自动恢复", "REQ-1",
                "vlc_stream_stalled");
            var aggregate = PreviewManager.BuildPreviewRecoverySummary(
                "车牌CJ", "持续故障", "REQ-1", "vlc_stream_stalled",
                count: 10, durationMs: 60000);
            var localSuccess = PreviewManager.BuildPreviewRecoverySummary(
                "车牌CJ", "已恢复", null, null);

            StringAssert.Contains(started, "RequestId=REQ-1");
            StringAssert.Contains(started, "ErrorCode=vlc_stream_stalled");
            StringAssert.Contains(aggregate, "Count=10");
            StringAssert.Contains(aggregate, "DurationMs=60000");
            StringAssert.Contains(aggregate, "ErrorCode=vlc_stream_stalled");
            Assert.AreEqual("车牌CJ预览已恢复", localSuccess);
            Assert.IsFalse(localSuccess.Contains("RequestId=<无>"));
            Assert.IsFalse(started.Contains("Operation="));
            Assert.IsFalse(started.Contains("RecoveryEpisodeId="));
        }

        [TestMethod]
        public void RecoveryRateLimitKeyDoesNotContainAttemptOrGeneration()
        {
            var key = PreviewManager.BuildPreviewRecoveryRateLimitKey(
                "PlateCJ_External", "VLC");

            StringAssert.Contains(key, "PreviewRecovery|VLC|PlateCJ_External");
            Assert.IsFalse(key.Contains("Attempt"));
            Assert.IsFalse(key.Contains("Generation"));
            Assert.IsFalse(key.Contains("WorkerId"));
        }

        [TestMethod]
        public void SelectRecoveryPreviewUrl_PrefersSavedExplicitUrl()
        {
            const string savedUrl = "http://127.0.0.1:18080/plate-live";
            const string requestedUrl = "http://127.0.0.1:18081/other-live";

            Assert.AreEqual(savedUrl,
                PreviewManager.SelectRecoveryPreviewUrl(savedUrl, requestedUrl));
        }

        [TestMethod]
        public void SelectRecoveryPreviewUrl_UsesRequestedUrlWhenExplicitUrlMissing()
        {
            const string requestedUrl = "http://127.0.0.1:18081/terminal-live";

            Assert.AreEqual(requestedUrl,
                PreviewManager.SelectRecoveryPreviewUrl(" ", requestedUrl));
        }
    }
}
