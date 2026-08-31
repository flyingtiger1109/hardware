using Microsoft.VisualStudio.TestTools.UnitTesting;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class LatestPlateFrameTests
    {
        [TestMethod]
        public void JpegFrameValidator_ReadsSofDimensions()
        {
            var jpeg = new byte[]
            {
                0xFF, 0xD8,
                0xFF, 0xE0, 0x00, 0x02,
                0xFF, 0xC0, 0x00, 0x0B,
                0x08, 0x04, 0x38, 0x07, 0x80, 0x03, 0x01, 0x11, 0x00,
                0xFF, 0xD9
            };

            Assert.IsTrue(JpegFrameValidator.TryGetDimensions(jpeg,
                out var width, out var height));
            Assert.AreEqual(1920, width);
            Assert.AreEqual(1080, height);
        }

        [TestMethod]
        public void JpegFrameValidator_RejectsNonJpeg()
        {
            Assert.IsFalse(JpegFrameValidator.TryGetDimensions(
                new byte[] { 0x01, 0x02, 0x03, 0x04 },
                out var width, out var height));
            Assert.AreEqual(0, width);
            Assert.AreEqual(0, height);
        }

        [TestMethod]
        public void LatestPlateFrameRoutes_AreRecognizedWithoutQueryString()
        {
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/cj/latest-frame?trace=1"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/rj2/latest-frame"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/rj3/latest-frame"));
            Assert.IsFalse(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/cj/start"));
        }
    }
}
