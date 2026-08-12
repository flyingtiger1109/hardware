using HZCYKJTHardWare.Proxy.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class VlcPreviewLayoutTests
    {
        [TestMethod]
        public void GetAspectRatioForLayout_DirectRenderUsesHostClientRatio()
        {
            var ratio = VlcPreviewPlayer.GetAspectRatioForLayout(
                directRenderTarget: true,
                hostW: 1280,
                hostH: 720,
                displayW: 1920,
                displayH: 1080);

            Assert.AreEqual("1280:720", ratio);
        }

        [TestMethod]
        public void GetAspectRatioForLayout_ChildWindowKeepsSourceRatio()
        {
            var ratio = VlcPreviewPlayer.GetAspectRatioForLayout(
                directRenderTarget: false,
                hostW: 1280,
                hostH: 720,
                displayW: 640,
                displayH: 480);

            Assert.AreEqual("640:480", ratio);
        }

        [TestMethod]
        public void LayoutRefreshInterval_RemainsResponsiveWithoutBusyPolling()
        {
            Assert.AreEqual(250, VlcPreviewController.LayoutRefreshIntervalMs);
        }
    }
}
