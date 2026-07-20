using System;
using System.IO;
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

        [TestMethod]
        public void GetLocalVlcDirectoryNames_KeepsArchitecturesSeparated()
        {
            CollectionAssert.AreEqual(
                new[] { "vlc-x64", "vlc" },
                VlcPreviewPlayer.GetLocalVlcDirectoryNames(true));
            CollectionAssert.AreEqual(
                new[] { "vlc" },
                VlcPreviewPlayer.GetLocalVlcDirectoryNames(false));
        }

        [TestMethod]
        public void IsPeMachineCompatible_RejectsCrossArchitectureLibraries()
        {
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "HZCYKJTHardWare-VlcPe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var x86Path = Path.Combine(tempDir, "x86.dll");
                var x64Path = Path.Combine(tempDir, "x64.dll");
                WriteMinimalPe(x86Path, 0x014c);
                WriteMinimalPe(x64Path, 0x8664);

                ushort machine;
                Assert.IsTrue(VlcPreviewPlayer.IsPeMachineCompatible(x86Path, false, out machine));
                Assert.AreEqual((ushort)0x014c, machine);
                Assert.IsFalse(VlcPreviewPlayer.IsPeMachineCompatible(x86Path, true, out machine));
                Assert.AreEqual((ushort)0x014c, machine);

                Assert.IsTrue(VlcPreviewPlayer.IsPeMachineCompatible(x64Path, true, out machine));
                Assert.AreEqual((ushort)0x8664, machine);
                Assert.IsFalse(VlcPreviewPlayer.IsPeMachineCompatible(x64Path, false, out machine));
                Assert.AreEqual((ushort)0x8664, machine);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        private static void WriteMinimalPe(string path, ushort machine)
        {
            var bytes = new byte[256];
            bytes[0] = 0x4d;
            bytes[1] = 0x5a;
            BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3c);
            bytes[0x80] = 0x50;
            bytes[0x81] = 0x45;
            BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
            File.WriteAllBytes(path, bytes);
        }
    }
}
