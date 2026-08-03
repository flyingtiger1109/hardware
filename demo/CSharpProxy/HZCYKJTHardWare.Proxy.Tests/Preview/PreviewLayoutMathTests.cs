using System.Drawing;
using HZCYKJTHardWare.Proxy.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class PreviewLayoutMathTests
    {
        [TestMethod]
        public void CalculateVideoBounds_Stretch_FillsHostForFourByThreeFrame()
        {
            var bounds = PreviewLayoutMath.CalculateVideoBounds(
                new Size(640, 480),
                new Size(1600, 900),
                PreviewScaleMode.Stretch);

            Assert.AreEqual(new Rectangle(0, 0, 1600, 900), bounds);
        }

        [TestMethod]
        public void CalculateVideoBounds_Stretch_FillsHostForSquareFingerprintFrame()
        {
            var bounds = PreviewLayoutMath.CalculateVideoBounds(
                new Size(640, 640),
                new Size(1600, 900),
                PreviewScaleMode.Stretch);

            Assert.AreEqual(new Rectangle(0, 0, 1600, 900), bounds);
        }

        [TestMethod]
        public void CalculateVideoBounds_Contain_PreservesEntireFourByThreeFrame()
        {
            var bounds = PreviewLayoutMath.CalculateVideoBounds(
                new Size(640, 480),
                new Size(1600, 900),
                PreviewScaleMode.Contain);

            Assert.AreEqual(new Rectangle(200, 0, 1200, 900), bounds);
        }

        [TestMethod]
        public void CalculateVideoBounds_Cover_FillsHostAndCentersCrop()
        {
            var bounds = PreviewLayoutMath.CalculateVideoBounds(
                new Size(640, 480),
                new Size(1600, 900),
                PreviewScaleMode.Cover);

            Assert.AreEqual(new Rectangle(0, -150, 1600, 1200), bounds);
        }

        [TestMethod]
        public void CalculateVideoBounds_Contain_PreservesSquareFingerprintFrame()
        {
            var bounds = PreviewLayoutMath.CalculateVideoBounds(
                new Size(640, 640),
                new Size(1600, 900),
                PreviewScaleMode.Contain);

            Assert.AreEqual(new Rectangle(350, 0, 900, 900), bounds);
        }

        [TestMethod]
        public void CalculateVideoBounds_InvalidSize_ReturnsEmpty()
        {
            var bounds = PreviewLayoutMath.CalculateVideoBounds(
                Size.Empty,
                new Size(1600, 900),
                PreviewScaleMode.Contain);

            Assert.AreEqual(Rectangle.Empty, bounds);
        }
    }
}
