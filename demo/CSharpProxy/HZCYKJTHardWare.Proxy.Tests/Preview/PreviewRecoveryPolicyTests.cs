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
