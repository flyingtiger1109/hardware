using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Terminal;
using HZCYKJTHardWare.Proxy.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Terminal
{
    [TestClass]
    public class TerminalHealthCheckerTests
    {
        [TestMethod]
        public void ParseResponse_MixedDeviceStates_PreservesAllStatusDetails()
        {
            const string json = @"{
  ""request_id"": ""req-20260326-001"",
  ""status"": ""ok"",
  ""data"": [
    { ""id"": ""ocr"", ""status"": ""online"", ""msg"": """" },
    { ""id"": ""nfc"", ""status"": ""online"", ""msg"": """" },
    { ""id"": ""fingerprint"", ""status"": ""starting"", ""msg"": ""recovery_local"" },
    { ""id"": ""iris"", ""status"": ""offline"", ""msg"": ""silence_timeout"" },
    { ""id"": ""face"", ""status"": ""abnormal"", ""msg"": ""recovery_local_failed"" }
  ]
}";
            var timestamp = new DateTime(2026, 3, 26, 10, 30, 0);

            var result = TerminalHealthChecker.ParseResponse(json, timestamp);

            Assert.AreEqual("req-20260326-001", result.RequestId);
            Assert.AreEqual("ok", result.ResponseStatus);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.IsFalse(result.IsHealthy);
            Assert.AreEqual(5, result.Devices.Count);
            Assert.AreEqual("recovery_local",
                result.Devices.Single(d => d.Id == "fingerprint").Message);
            Assert.AreEqual("silence_timeout",
                result.Devices.Single(d => d.Id == "iris").Message);
            Assert.AreEqual("recovery_local_failed",
                result.Devices.Single(d => d.Id == "face").Message);
        }

        [TestMethod]
        public void ParseResponse_MissingRequiredDevice_AddsUnknownPlaceholder()
        {
            const string json = @"{
  ""request_id"": ""req-partial"",
  ""status"": ""ok"",
  ""data"": [
    { ""id"": ""ocr"", ""status"": ""online"", ""msg"": """" }
  ]
}";

            var result = TerminalHealthChecker.ParseResponse(json, DateTime.Now);

            Assert.IsFalse(result.IsHealthy);
            Assert.AreEqual(5, result.Devices.Count);
            var face = result.Devices.Single(d => d.Id == "face");
            Assert.AreEqual("unknown", face.Status);
            Assert.AreEqual("not_reported", face.Message);
        }

        [TestMethod]
        public void ParseResponse_MissingData_IsNotReportedAsHealthy()
        {
            const string json = @"{
  ""request_id"": ""req-no-data"",
  ""status"": ""ok""
}";

            var result = TerminalHealthChecker.ParseResponse(json, DateTime.Now);

            Assert.IsFalse(result.IsHealthy);
            StringAssert.Contains(result.ErrorMessage, "缺少 data");
        }

        [TestMethod]
        public void Presentations_TranslateProtocolMessagesToOperatorCopy()
        {
            AssertPresentation("online", "", HardwareVisualState.Online,
                "正常", "设备连接稳定");
            AssertPresentation("starting", "recovery_local", HardwareVisualState.Starting,
                "启动中", "正在恢复连接…");
            AssertPresentation("offline", "silence_timeout", HardwareVisualState.Offline,
                "离线", "设备暂未响应");
            AssertPresentation("abnormal", "recovery_local_failed", HardwareVisualState.Abnormal,
                "异常", "自动恢复失败，请检查设备");
        }

        [TestMethod]
        public void GetNextDelay_UsesBoundedBackoffAndFallsBackToSlowProbe()
        {
            Assert.AreEqual(5000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { ErrorMessage = "终端连接失败" }, 0));
            Assert.AreEqual(10000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { ErrorMessage = "终端连接失败" }, 1));
            Assert.AreEqual(20000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { ErrorMessage = "终端连接失败" }, 2));
            Assert.AreEqual(40000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { ErrorMessage = "终端连接失败" }, 3));
            Assert.AreEqual(60000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { ErrorMessage = "终端连接失败" }, 4));
            Assert.AreEqual(5 * 60 * 1000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { ErrorMessage = "终端连接失败" }, 5));
            Assert.AreEqual(5000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { IsHealthy = false }, 0));
            Assert.AreEqual(5 * 60 * 1000, TerminalHealthChecker.GetNextDelayMs(
                new HealthStatus { IsHealthy = true }, 5));
        }

        [TestMethod]
        public void RequestCheck_ManualRefreshDoesNotResetBackoffAttempt()
        {
            var checker = new TerminalHealthChecker(null, null, null, null);
            var retryAttemptField = typeof(TerminalHealthChecker).GetField(
                "_retryAttempt", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(retryAttemptField);

            retryAttemptField.SetValue(checker, 5);
            checker.RequestCheck(resetRetryAttempt: false);
            Assert.AreEqual(5, retryAttemptField.GetValue(checker),
                "手动刷新只触发一次检测，不应让失败状态重新从 5 秒退避开始");

            checker.RequestCheck();
            Assert.AreEqual(0, retryAttemptField.GetValue(checker),
                "内部刷新仍应能在终端切换等场景重置退避链路");

            checker.Dispose();
        }

        [TestMethod]
        public async Task StopAsync_BeforeInitialPoll_IsIdempotentAndPreventsRestart()
        {
            var checker = new TerminalHealthChecker(null, null, _ => { }, null);

            checker.Start();
            await checker.StopAsync(1000);
            await checker.StopAsync(1000);
            checker.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => checker.Start());
        }

        private static void AssertPresentation(
            string status,
            string message,
            HardwareVisualState expectedState,
            string expectedStatusText,
            string expectedMessageText)
        {
            var presentation = HardwareHealthPresentation.From(new DeviceHealth
            {
                Status = status,
                Message = message
            });

            Assert.AreEqual(expectedState, presentation.State);
            Assert.AreEqual(expectedStatusText, presentation.StatusText);
            Assert.AreEqual(expectedMessageText, presentation.MessageText);
        }
    }
}
