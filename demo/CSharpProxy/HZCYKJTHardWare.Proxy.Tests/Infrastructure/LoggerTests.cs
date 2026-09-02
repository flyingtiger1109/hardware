using System;
using System.IO;
using System.Text;
using HZCYKJTHardWare.Proxy.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Infrastructure
{
    [TestClass]
    public class LoggerTests
    {
        [TestMethod]
        public void CreateSharedWriter_AllowsLiveReadBeforeWriterCloses()
        {
            var filePath = Path.Combine(Path.GetTempPath(),
                "proxy_logger_share_" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                using (var writer = Logger.CreateSharedWriter(filePath))
                {
                    writer.WriteLine("live-readable-marker");
                    writer.Flush();

                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        StringAssert.Contains(reader.ReadToEnd(), "live-readable-marker");
                    }
                }
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }

        [TestMethod]
        public void Flush_DrainsQueuedEntryAndUpdatesHealthState()
        {
            var marker = "logger-health-" + Guid.NewGuid().ToString("N");
            var failuresBefore = Logger.WriteFailureCount;

            Logger.Info(marker);
            Logger.Flush(3000);

            Assert.AreEqual(0, Logger.PendingCount);
            Assert.AreEqual(failuresBefore, Logger.WriteFailureCount);
            Assert.IsTrue(Logger.CurrentFileLength > 0);
            Assert.IsTrue(Logger.LastFlushAgeMs >= 0);

            var logPath = Path.Combine(Logger.LogDirectory,
                "HZCYKJTHardWareExe_Logs_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                StringAssert.Contains(reader.ReadToEnd(), marker);
            }
        }

        [TestMethod]
        public void NormalizeForDisplay_UsesConcreteChineseModuleAndLevel()
        {
            Assert.AreEqual(
                "[预览][信息] 外部预览已启动",
                Logger.NormalizeForDisplay("[预览管理] 外部预览已启动"));
            Assert.AreEqual(
                "[预览][错误] HTTP MJPEG帧渲染失败",
                Logger.NormalizeForDisplay("[预览租约][error] HTTP MJPEG帧渲染失败"));
            Assert.AreEqual(
                "[人脸抓拍][信息] 人脸抓拍已受理",
                Logger.NormalizeForDisplay("人脸抓拍已受理"));
            Assert.AreEqual(
                "[预览][信息] 预览请求已受理",
                Logger.FormatModuleMessage("预览请求", "info", "预览请求已受理"));
            Assert.AreEqual(
                "[人脸抓拍][信息] 人脸抓拍已受理",
                Logger.NormalizeForDisplay("[DLL请求] 人脸抓拍已受理"));
            Assert.AreEqual(
                "[健康检查][信息] HTTP GET /ping完成",
                Logger.NormalizeForDisplay("[HTTP请求] HTTP GET /ping完成"));
        }

        [TestMethod]
        public void CanonicalOperationMapping_CoversAllKnownDllExports()
        {
            var cases = new[]
            {
                new[] { "HZCYKJTHardWare_InitSdk", "InitSdk" },
                new[] { "HZCYKJTHardWare_ReleaseSdk", "ReleaseSdk" },
                new[] { "HZCYKJTHardWare_SwitchTerminal", "SwitchTerminal" },
                new[] { "HZCYKJTHardWare_StartProcess", "StartProcess" },
                new[] { "HZCYKJTHardWare_EndProcess", "EndProcess" },
                new[] { "HZCYKJTHardWare_StartCameraPreview", "StartCameraPreview" },
                new[] { "HZCYKJTHardWare_StopCameraPreview", "StopCameraPreview" },
                new[] { "HZCYKJTHardWare_StartFingerprintPreview", "StartFingerprintPreview" },
                new[] { "HZCYKJTHardWare_StopFingerprintPreview", "StopFingerprintPreview" },
                new[] { "HZCYKJTHardWare_StartIrisPreview", "StartIrisPreview" },
                new[] { "HZCYKJTHardWare_StopIrisPreview", "StopIrisPreview" },
                new[] { "HZCYKJTHardWare_StartPlatePreviewCJ", "StartPlatePreviewCJ" },
                new[] { "HZCYKJTHardWare_StopPlatePreviewCJ", "StopPlatePreviewCJ" },
                new[] { "HZCYKJTHardWare_StartPlatePreviewRJ2", "StartPlatePreviewRJ2" },
                new[] { "HZCYKJTHardWare_StopPlatePreviewRJ2", "StopPlatePreviewRJ2" },
                new[] { "HZCYKJTHardWare_StartPlatePreviewRJ3", "StartPlatePreviewRJ3" },
                new[] { "HZCYKJTHardWare_StopPlatePreviewRJ3", "StopPlatePreviewRJ3" },
                new[] { "HZCYKJTHardWare_SaveLatestPlateFrame", "SaveLatestPlateFrame" },
                new[] { "HZCYKJTHardWare_CaptureCameraImage", "CaptureFace" },
                new[] { "HZCYKJTHardWare_CaptureFingerprintImage", "CaptureFingerprint" },
                new[] { "HZCYKJTHardWare_CaptureIrisImage", "CaptureIris" },
                new[] { "HZCYKJTHardWare_RequestOCR", "RequestOCR" },
                new[] { "HZCYKJTHardWare_RequestNfcCard", "RequestNfcCard" },
                new[] { "HZCYKJTHardWare_RequestAuthorize", "Authorize" },
                new[] { "HZCYKJTHardWare_RegisterEventCallback", "RegisterEventCallback" }
            };

            foreach (var item in cases)
                Assert.AreEqual(item[1], Logger.CanonicalOperationName(item[0]), item[0]);

            Assert.AreEqual("CaptureFace", Logger.CanonicalOperationName("POST /capture/face"));
            Assert.AreEqual("Authorize", Logger.CanonicalOperationName("/resources/protocol/request"));
        }

        [TestMethod]
        public void KnownOperationInLegacyInterfaceMessage_UsesConcreteModule()
        {
            var message = Logger.NormalizeForDisplay(
                "[接口][信息] Operation=HZCYKJTHardWare_CaptureCameraImage RequestId=REQ-1 Result=Success");

            Assert.AreEqual(
                "[人脸抓拍][信息] Operation=CaptureFace RequestId=REQ-1 Result=Success",
                message);
        }

        [TestMethod]
        public void OperationModuleMapping_CoversAllKnownDllExports()
        {
            var cases = new[]
            {
                new[] { "HZCYKJTHardWare_InitSdk", "SDK生命周期" },
                new[] { "HZCYKJTHardWare_ReleaseSdk", "SDK生命周期" },
                new[] { "HZCYKJTHardWare_SwitchTerminal", "终端切换" },
                new[] { "HZCYKJTHardWare_StartProcess", "流程控制" },
                new[] { "HZCYKJTHardWare_EndProcess", "流程控制" },
                new[] { "HZCYKJTHardWare_StartCameraPreview", "预览" },
                new[] { "HZCYKJTHardWare_StopCameraPreview", "预览" },
                new[] { "HZCYKJTHardWare_StartFingerprintPreview", "预览" },
                new[] { "HZCYKJTHardWare_StopFingerprintPreview", "预览" },
                new[] { "HZCYKJTHardWare_StartIrisPreview", "预览" },
                new[] { "HZCYKJTHardWare_StopIrisPreview", "预览" },
                new[] { "HZCYKJTHardWare_StartPlatePreviewCJ", "预览" },
                new[] { "HZCYKJTHardWare_StopPlatePreviewCJ", "预览" },
                new[] { "HZCYKJTHardWare_StartPlatePreviewRJ2", "预览" },
                new[] { "HZCYKJTHardWare_StopPlatePreviewRJ2", "预览" },
                new[] { "HZCYKJTHardWare_StartPlatePreviewRJ3", "预览" },
                new[] { "HZCYKJTHardWare_StopPlatePreviewRJ3", "预览" },
                new[] { "HZCYKJTHardWare_SaveLatestPlateFrame", "车牌抓帧" },
                new[] { "HZCYKJTHardWare_CaptureCameraImage", "人脸抓拍" },
                new[] { "HZCYKJTHardWare_CaptureFingerprintImage", "指纹抓拍" },
                new[] { "HZCYKJTHardWare_CaptureIrisImage", "虹膜抓拍" },
                new[] { "HZCYKJTHardWare_RequestOCR", "证件识别" },
                new[] { "HZCYKJTHardWare_RequestNfcCard", "NFC读卡" },
                new[] { "HZCYKJTHardWare_RequestAuthorize", "授权" },
                new[] { "HZCYKJTHardWare_RegisterEventCallback", "终端回调" }
            };

            foreach (var item in cases)
            {
                var message = Logger.NormalizeForDisplay(
                    "[接口][信息] Operation=" + item[0] +
                    " RequestId=REQ-MAP Result=Success");
                var expected = "[" + item[1] + "][信息] Operation=" +
                    Logger.CanonicalOperationName(item[0]) +
                    " RequestId=REQ-MAP Result=Success";
                Assert.AreEqual(expected, message, item[0]);
            }
        }

        [TestMethod]
        public void BusinessContext_UsesShortOperationAndStableResultFields()
        {
            var fields = Logger.FormatContextMessage(
                "HZCYKJTHardWare_SwitchTerminal",
                requestId: "SWITCH-1",
                result: "成功",
                durationMs: 16);

            StringAssert.Contains(fields, "Operation=SwitchTerminal");
            StringAssert.Contains(fields, "RequestId=SWITCH-1");
            StringAssert.Contains(fields, "Result=Success");
            StringAssert.Contains(fields, "DurationMs=16");
            Assert.IsFalse(fields.Contains("HZCYKJTHardWare_SwitchTerminal"));
            Assert.IsFalse(fields.Contains("入口"));
            Assert.IsFalse(fields.Contains("出口"));
        }

        [TestMethod]
        public void BusinessMessageDoesNotUseEntryExit()
        {
            var message = Logger.FormatModuleMessage(LogModules.TerminalSwitch, "信息",
                "终端切换成功：当前=左通道 " + Logger.FormatContextMessage(
                    "SwitchTerminal", requestId: "SWITCH-1", result: "Success",
                    durationMs: 16));

            StringAssert.Contains(message, "终端切换成功");
            Assert.IsFalse(message.Contains("入口"));
            Assert.IsFalse(message.Contains("出口"));
            Assert.IsFalse(message.Contains("POST完成"));
            Assert.IsFalse(message.Contains("DurationMs=0"));
        }

        [TestMethod]
        public void ResourceDisplayName_UsesChineseBusinessNames()
        {
            Assert.AreEqual("人脸", Logger.ResourceDisplayName("face_image"));
            Assert.AreEqual("摄像头/第三方", Logger.ResourceDisplayName("Camera_External"));
            Assert.AreEqual("授权", Logger.ResourceDisplayName("authorization"));
        }

        [TestMethod]
        public void MessageLevelFilter_IsSharedByFileAndUiDecision()
        {
            try
            {
                Logger.SetMinLevel("info");
                Assert.IsFalse(Logger.IsMessageEnabled("[预览][调试] HTTP收发明细"));
                Assert.IsTrue(Logger.IsMessageEnabled("[预览][信息] 预览已启动"));
                Assert.IsTrue(Logger.IsMessageEnabled("[预览][错误] 预览启动失败"));

                Logger.SetMinLevel("debug");
                Assert.IsTrue(Logger.IsMessageEnabled("[预览][调试] HTTP收发明细"));
            }
            finally
            {
                Logger.SetMinLevel("info");
            }
        }

        [TestMethod]
        public void SanitizeLargePayload_OnlyKeepsAllowedScalarsAndNeverWritesRawBody()
        {
            var payload = "{\"request_id\":\"REQ-LOG-1\",\"status\":\"ok\"," +
                          "\"message\":\"完成\",\"image_base64\":\"BASE64_SECRET\"," +
                          "\"nested\":{\"raw\":\"RAW_SECRET\"}}";

            var sanitized = Logger.SanitizeLargePayloadForLog(payload);

            StringAssert.Contains(sanitized, "RequestId=REQ-LOG-1");
            StringAssert.Contains(sanitized, "status=ok");
            StringAssert.Contains(sanitized, "message=完成");
            StringAssert.Contains(sanitized, "omitted chars=");
            Assert.IsFalse(sanitized.Contains("BASE64_SECRET"));
            Assert.IsFalse(sanitized.Contains("RAW_SECRET"));
            Assert.IsFalse(sanitized.Contains(payload));
        }

        [TestMethod]
        public void SanitizeLargePayload_InvalidJsonFallsBackToLengthAndRequestIdOnly()
        {
            var sanitized = Logger.SanitizeLargePayloadForLog(
                "{invalid-body-with-secret=DO_NOT_LOG", "REQ-LOG-2");

            StringAssert.Contains(sanitized, "RequestId=REQ-LOG-2");
            StringAssert.Contains(sanitized, "omitted chars=");
            Assert.IsFalse(sanitized.Contains("DO_NOT_LOG"));
        }

        [TestMethod]
        public void SanitizeUrlForLog_MasksAuthorityAndCredentialQueryValues()
        {
            var sanitized = Logger.SanitizeUrlForLog(
                "rtsp://user:password@example.test/live?token=abc123&channel=1");
            var pathSanitized = Logger.SanitizeUrlForLog("/preview?auth=secret&channel=1");

            StringAssert.Contains(sanitized, "***:***@example.test");
            StringAssert.Contains(sanitized, "token=***");
            StringAssert.Contains(sanitized, "channel=1");
            Assert.IsFalse(sanitized.Contains("password"));
            Assert.IsFalse(sanitized.Contains("abc123"));
            StringAssert.Contains(pathSanitized, "auth=***");
            Assert.IsFalse(pathSanitized.Contains("secret"));
        }

        [TestMethod]
        public void RateLimiter_EmitsFirstAndWindowEndSummaryOnly()
        {
            var limiter = new LogRateLimiter(TimeSpan.FromMinutes(1));
            var now = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);

            var first = limiter.Record("T1|/ocr|12029", "连接超时", now);
            var repeated = limiter.Record("T1|/ocr|12029", "连接超时", now.AddSeconds(1));
            var nextWindow = limiter.Record("T1|/ocr|12029", "连接被拒绝", now.AddSeconds(61));

            Assert.IsTrue(first.EmitCurrent);
            Assert.IsNull(first.WindowSummary);
            Assert.IsFalse(repeated.EmitCurrent);
            Assert.IsTrue(nextWindow.EmitCurrent);
            Assert.IsFalse(nextWindow.WindowSummary.Contains("RateLimitWindowEnd"));
            Assert.IsFalse(nextWindow.WindowSummary.Contains("Count="));
            Assert.IsFalse(nextWindow.WindowSummary.Contains("FirstTime="));
            Assert.IsFalse(nextWindow.WindowSummary.Contains("LastTime="));
            Assert.IsFalse(nextWindow.WindowSummary.Contains("LastError="));
            StringAssert.Contains(nextWindow.WindowSummary, "类别=连接失败");
            StringAssert.Contains(nextWindow.WindowSummary, "次数=2");
            StringAssert.Contains(nextWindow.WindowSummary, "首次=");
            StringAssert.Contains(nextWindow.WindowSummary, "最近=");
            StringAssert.Contains(nextWindow.WindowSummary, "最近错误=连接超时");
            Assert.AreEqual("连接被拒绝", nextWindow.CurrentError);

            var merged = LogRateLimiter.FormatMergedMessage(nextWindow, "连接被拒绝");
            Assert.AreEqual(1, CountOccurrences(merged, "重复故障汇总"));
            Assert.AreEqual(1, CountOccurrences(merged, "本次错误="));
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var offset = 0;
            while (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(value))
            {
                var index = text.IndexOf(value, offset, StringComparison.Ordinal);
                if (index < 0) break;
                count++;
                offset = index + value.Length;
            }
            return count;
        }
    }
}
