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
        public void ShouldValidatePreviewUrl_SkipsHttpMjpegUrls()
        {
            Assert.IsFalse(PreviewManager.ShouldValidatePreviewUrl("http://192.168.20.30/live.mjpg"));
            Assert.IsFalse(PreviewManager.ShouldValidatePreviewUrl("HTTPS://terminal/live"));
            Assert.IsTrue(PreviewManager.ShouldValidatePreviewUrl("rtsp://192.168.20.30/live"));
        }
    }
}
