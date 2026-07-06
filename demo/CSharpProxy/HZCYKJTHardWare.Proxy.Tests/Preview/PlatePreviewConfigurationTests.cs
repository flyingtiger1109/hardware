using Microsoft.VisualStudio.TestTools.UnitTesting;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class PlatePreviewConfigurationTests
    {
        [TestMethod]
        public void GetPlatePreviewUrl_ReturnsEmpty_WhenDisabled()
        {
            var config = new AppConfig
            {
                PlatePreviewCJ = new PlatePreviewCameraConfig
                {
                    Enabled = false,
                    Host = "192.168.20.40"
                }
            };

            Assert.AreEqual("", config.GetPlatePreviewUrl("cj"));
        }

        [TestMethod]
        public void GetPlatePreviewUrl_EncodesCredentials_AndUsesConfiguredChannel()
        {
            var config = new AppConfig
            {
                PlatePreviewRJ2 = new PlatePreviewCameraConfig
                {
                    Enabled = true,
                    Host = "192.168.20.40",
                    Port = 554,
                    Username = "user@lane",
                    Password = "p:a/ss",
                    StreamChannel = 102
                }
            };

            Assert.AreEqual(
                "rtsp://user%40lane:p%3Aa%2Fss@192.168.20.40:554/Streaming/Channels/102",
                config.GetPlatePreviewUrl("rj2"));
        }

        [TestMethod]
        public void GetPlatePreviewUrl_KeepsCameraConfigurationsIndependent()
        {
            var config = new AppConfig
            {
                PlatePreviewCJ = new PlatePreviewCameraConfig
                {
                    Enabled = true,
                    Host = "192.168.20.41"
                },
                PlatePreviewRJ3 = new PlatePreviewCameraConfig
                {
                    Enabled = true,
                    Host = "192.168.20.43"
                }
            };

            Assert.AreEqual(
                "rtsp://192.168.20.41:554/Streaming/Channels/101",
                config.GetPlatePreviewUrl("cj"));
            Assert.AreEqual("", config.GetPlatePreviewUrl("rj2"));
            Assert.AreEqual(
                "rtsp://192.168.20.43:554/Streaming/Channels/101",
                config.GetPlatePreviewUrl("rj3"));
            Assert.AreEqual("", config.GetPlatePreviewUrl("unknown"));
        }

        [TestMethod]
        public void SanitizeUrlForLog_RemovesRtspCredentials()
        {
            var sanitized = VlcPreviewPlayer.SanitizeUrlForLog(
                "rtsp://admin:secret@192.168.20.40:554/Streaming/Channels/101");

            Assert.AreEqual(
                "rtsp://***:***@192.168.20.40:554/Streaming/Channels/101",
                sanitized);
            Assert.IsFalse(sanitized.Contains("admin"));
            Assert.IsFalse(sanitized.Contains("secret"));
        }
    }
}
