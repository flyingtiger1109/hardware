using System.Collections.Generic;
using System.Linq;
using HZCYKJTHardWare.Proxy.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using HZCYKJTHardWare.Proxy.Core;

namespace HZCYKJTHardWare.Proxy.Tests.Infrastructure
{
    [TestClass]
    public class DeviceCapabilityManagerTests
    {
        [TestMethod]
        public void Mode1_SupportsAllDeclaredCapabilities()
        {
            var manager = new DeviceCapabilityManager(DeviceMode.Full);
            foreach (DeviceCapability capability in
                System.Enum.GetValues(typeof(DeviceCapability)))
                Assert.IsTrue(manager.IsSupported(capability), capability.ToString());
        }

        [TestMethod]
        public void Mode2_SupportsOnlyRj2AndRj3()
        {
            var manager = new DeviceCapabilityManager(DeviceMode.RjCameraOnly);
            Assert.IsTrue(manager.IsSupported(DeviceCapability.PlateRJ2));
            Assert.IsTrue(manager.IsSupported(DeviceCapability.PlateRJ3));
            Assert.IsFalse(manager.IsSupported(DeviceCapability.PlateCJ));
            Assert.IsFalse(manager.IsSupported(DeviceCapability.OCR));
            Assert.IsFalse(manager.IsSupported(DeviceCapability.TerminalControl));
        }

        [TestMethod]
        public void MissingOrInvalidMode_FallsBackToMode1WithWarning()
        {
            var warnings = new List<string>();
            Assert.AreEqual(DeviceMode.Full,
                AppConfig.ResolveDeviceMode(null, warnings.Add));
            Assert.AreEqual(DeviceMode.Full,
                AppConfig.ResolveDeviceMode(new JValue(3), warnings.Add));
            Assert.AreEqual(DeviceMode.Full,
                AppConfig.ResolveDeviceMode(new JValue("2"), warnings.Add));
            Assert.AreEqual(3, warnings.Count);
        }

        [TestMethod]
        public void DeviceModeName_UsesCurrentModeMappingAndStableFallback()
        {
            var names = JObject.Parse(
                "{\"1\":\"完整设备模式\",\"2\":\"入境模式\"}");
            Assert.AreEqual("完整设备模式",
                AppConfig.ResolveDeviceModeName(names, DeviceMode.Full));
            Assert.AreEqual("入境模式",
                AppConfig.ResolveDeviceModeName(names, DeviceMode.RjCameraOnly));
            Assert.AreEqual("RJ2/RJ3 镜头模式",
                AppConfig.ResolveDeviceModeName(null, DeviceMode.RjCameraOnly));
        }

        [TestMethod]
        public void DefaultTerminalIndex_RejectsUnsupportedValues()
        {
            var warnings = new List<string>();
            Assert.AreEqual(2, AppConfig.ResolveTerminalIndex(2, warnings.Add));
            Assert.AreEqual(1, AppConfig.ResolveTerminalIndex(3, warnings.Add));
            Assert.AreEqual(1, AppConfig.ResolveTerminalIndex(null, warnings.Add));
            Assert.AreEqual(1, warnings.Count);
        }

        [TestMethod]
        public void TerminalConfiguration_MissingDeviceListKeepsLegacyDefaults()
        {
            Assert.IsTrue(AppConfig.ResolveTerminalConfigured(null, 1));
            Assert.IsTrue(AppConfig.ResolveTerminalConfigured(null, 2));
        }

        [TestMethod]
        public void TerminalConfiguration_TracksOnlyDirectionsPresentInDeviceList()
        {
            var leftOnly = JArray.Parse(
                "[{\"index\":1,\"name\":\"左通道\",\"host_suffix\":30}]");
            Assert.IsTrue(AppConfig.ResolveTerminalConfigured(leftOnly, 1));
            Assert.IsFalse(AppConfig.ResolveTerminalConfigured(leftOnly, 2));

            var rightOnly = JArray.Parse(
                "[{\"index\":2,\"name\":\"右通道\",\"host_suffix\":31}]");
            Assert.IsFalse(AppConfig.ResolveTerminalConfigured(rightOnly, 1));
            Assert.IsTrue(AppConfig.ResolveTerminalConfigured(rightOnly, 2));
        }

        [TestMethod]
        public void TerminalConfiguration_EmptyIpSuffixMeansUnconfigured()
        {
            var emptyValues = JArray.Parse(
                "[{\"index\":1,\"host_suffix\":\"\"},{\"index\":2,\"host_suffix\":0}]");

            Assert.IsFalse(AppConfig.ResolveTerminalConfigured(emptyValues, 1));
            Assert.IsFalse(AppConfig.ResolveTerminalConfigured(emptyValues, 2));
            Assert.AreEqual(0, AppConfig.ResolveHostSuffix(emptyValues[0]));
            Assert.AreEqual(0, AppConfig.ResolveHostSuffix(emptyValues[1]));
        }

        [TestMethod]
        public void TerminalConfiguration_MalformedEmptyHostSuffix_IsNormalizedToNull()
        {
            var malformed = "{\"terminal\":{\"auto_subnet_devices\":[" +
                "{\"index\":1,\"host_suffix\":}," +
                "{\"index\":2,\"host_suffix\":}" +
                "]}}";

            var normalized = AppConfig.NormalizeEmptyHostSuffix(malformed);
            var terminal = JObject.Parse(normalized)["terminal"];
            var devices = terminal["auto_subnet_devices"];

            Assert.IsFalse(AppConfig.ResolveTerminalConfigured(devices, 1));
            Assert.IsFalse(AppConfig.ResolveTerminalConfigured(devices, 2));
        }

        [TestMethod]
        public void RouteMapping_UsesSameCapabilityModelAsBusinessAndUi()
        {
            var manager = new DeviceCapabilityManager(DeviceMode.RjCameraOnly);
            Assert.IsTrue(manager.TryGetRequiredCapability("/preview/plate/rj2/start",
                out var rj2));
            Assert.AreEqual(DeviceCapability.PlateRJ2, rj2);
            Assert.IsTrue(manager.TryGetRequiredCapability("/ocr", out var ocr));
            Assert.AreEqual(DeviceCapability.OCR, ocr);
            Assert.IsTrue(manager.TryGetPreviewCapability("plate_rj3", out var rj3));
            Assert.AreEqual(DeviceCapability.PlateRJ3, rj3);
        }

        [TestMethod]
        public void NotSupportedResult_IsStableAndExplicit()
        {
            var manager = new DeviceCapabilityManager(DeviceMode.RjCameraOnly);
            var result = JObject.Parse(manager.BuildNotSupportedResult("/ocr",
                DeviceCapability.OCR));
            Assert.IsTrue(result.Value<bool>("error"));
            Assert.AreEqual("not_supported", result.Value<string>("code"));
            Assert.AreEqual(2, result.Value<int>("device_mode"));
            Assert.AreEqual("OCR", result.Value<string>("capability"));
        }

        [TestMethod]
        public void Mode2_QueueManager_DoesNotStartBusinessWorkers()
        {
            using (var queues = new QueueManager(
                new DeviceCapabilityManager(DeviceMode.RjCameraOnly)))
            {
                Assert.IsFalse(queues.SwitchQueue.WorkerStarted);
                Assert.IsFalse(queues.FaceCaptureQueue.WorkerStarted);
                Assert.IsFalse(queues.FingerprintCaptureQueue.WorkerStarted);
                Assert.IsFalse(queues.IrisQueue.WorkerStarted);
                Assert.IsFalse(queues.OcrQueue.WorkerStarted);
                Assert.IsFalse(queues.NfcQueue.WorkerStarted);
                Assert.IsFalse(queues.AuthorizeQueue.WorkerStarted);
            }
        }
    }
}
