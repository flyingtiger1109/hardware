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
                "[接口][调试] Operation=HZCYKJTHardWare_CaptureCameraImage RequestId=REQ-1 Result=Success");

            Assert.AreEqual(
                "[人脸抓拍][调试] Operation=CaptureFace RequestId=REQ-1 Result=Success",
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
                    "[接口][调试] Operation=" + item[0] +
                    " RequestId=REQ-MAP Result=Success");
                var expected = "[" + item[1] + "][调试] Operation=" +
                    Logger.CanonicalOperationName(item[0]) +
                    " RequestId=REQ-MAP Result=Success";
                Assert.AreEqual(expected, message, item[0]);
            }
        }

        [TestMethod]
        public void ProductionInfo_OnlyKeepsChineseResultAndRequestId()
        {
            var message = Logger.NormalizeForDisplay(
                "[接口][信息] 人脸图片抓拍成功：Operation=CaptureFace " +
                "RequestId=FACE-1 Result=Success ErrorCode=none DurationMs=18 " +
                "Stage=SaveFile Bytes=123 Width=640 Height=480 FrameAgeMs=2 Source=Cache");

            Assert.AreEqual(
                "[人脸抓拍][信息] 人脸图片抓拍成功：RequestId=FACE-1",
                message);
            Assert.IsFalse(message.Contains("Operation="));
            Assert.IsFalse(message.Contains("Result=Success"));
            Assert.IsFalse(message.Contains("ErrorCode=none"));
            Assert.IsFalse(message.Contains("DurationMs="));
            Assert.IsFalse(message.Contains("Stage="));
            Assert.IsFalse(message.Contains("Bytes="));
            Assert.IsFalse(message.Contains("Width="));
            Assert.IsFalse(message.Contains("Height="));
            Assert.IsFalse(message.Contains("FrameAgeMs="));
            Assert.IsFalse(message.Contains("Source="));
        }

        [TestMethod]
        public void ProductionSuccess_FileOperationKeepsFinalSavePathWithSpaces()
        {
            const string savePath = @"D:\抓拍图片\车牌 CJ\Path=2026-09-03 10-20-30.jpg";
            var message = Logger.NormalizeForDisplay(
                "[车牌抓帧][信息] 车牌CJ抓拍成功：Operation=SaveLatestPlateFrame " +
                "RequestId=CAP-001 Result=Success SavePath=" + savePath +
                " DurationMs=80 Width=1920 Height=1080");

            Assert.AreEqual(
                "[车牌抓帧][信息] 车牌CJ抓拍成功：RequestId=CAP-001 保存路径=" + savePath,
                message);
            Assert.IsFalse(message.Contains("Operation="));
            Assert.IsFalse(message.Contains("DurationMs="));
            Assert.IsFalse(message.Contains("Width="));
            Assert.IsFalse(message.Contains("Height="));
        }

        [TestMethod]
        public void ProductionSuccess_FilePathAliasesAreNormalizedToOneField()
        {
            var aliases = new[] { "SavedPath", "OutputPath", "FilePath", "Path" };
            const string savePath = @"D:\Capture\Face Image\face 001.jpg";

            foreach (var alias in aliases)
            {
                var message = Logger.NormalizeForDisplay(
                    "[人脸抓拍][信息] 图片保存成功：Operation=CaptureFace " +
                    "RequestId=FACE-001 Result=Success " + alias + "=" + savePath +
                    " Bytes=123");

                Assert.AreEqual(
                    "[人脸抓拍][信息] 图片保存成功：RequestId=FACE-001 保存路径=" + savePath,
                    message, alias);
                Assert.AreEqual(1, CountOccurrences(message, "保存路径="));
            }
        }

        [TestMethod]
        public void ProductionFailure_FilePathIsNotPrinted()
        {
            const string savePath = @"D:\Capture\CJ\001.jpg";
            var message = Logger.NormalizeForDisplay(
                "[车牌抓帧][错误] 车牌CJ抓拍失败：Operation=SaveLatestPlateFrame " +
                "RequestId=CAP-001 Result=Failed ErrorCode=frame_not_ready " +
                "SavePath=" + savePath);

            Assert.AreEqual(
                "[车牌抓帧][错误] 车牌CJ抓拍失败：RequestId=CAP-001 " +
                "ErrorCode=frame_not_ready",
                message);
            Assert.IsFalse(message.Contains("保存路径="));
            Assert.IsFalse(message.Contains(savePath));
        }

        [TestMethod]
        public void ProductionSuccess_LocalWithoutRequestIdStillKeepsSavePath()
        {
            const string savePath = @"D:\抓拍图片\local face.jpg";
            var message = Logger.NormalizeForDisplay(
                "[人脸抓拍][信息] 图片保存成功：Operation=CaptureFace " +
                "RequestId=<无> Result=Success SavePath=" + savePath);

            Assert.AreEqual(
                "[人脸抓拍][信息] 图片保存成功：保存路径=" + savePath,
                message);
            Assert.IsFalse(message.Contains("RequestId=<无>"));
        }

        [TestMethod]
        public void ProductionFailure_OnlyKeepsRequestIdAndErrorCode()
        {
            var message = Logger.NormalizeForDisplay(
                "[预览][错误] 摄像头预览启动失败：Operation=StartCameraPreview " +
                "RequestId=PREVIEW-1 Result=Failed ErrorCode=12029 DurationMs=2000 " +
                "Stage=StartPlayer StackTrace=hidden");

            Assert.AreEqual(
                "[预览][错误] 摄像头预览启动失败：RequestId=PREVIEW-1 ErrorCode=12029",
                message);
            Assert.IsFalse(message.Contains("DurationMs="));
            Assert.IsFalse(message.Contains("Stage="));
            Assert.IsFalse(message.Contains("StackTrace="));
        }

        [TestMethod]
        public void LegacyRequestIdField_IsCanonicalizedWithoutDuplicateOutput()
        {
            var message = Logger.NormalizeForDisplay(
                "[预览][调试] VLC预览恢复尝试：request_id=REQ-LEGACY " +
                "Attempt=1");

            StringAssert.Contains(message, "RequestId=REQ-LEGACY");
            Assert.AreEqual(1, CountOccurrences(message, "RequestId=REQ-LEGACY"));
            Assert.IsFalse(message.Contains("request_id="));
        }

        [TestMethod]
        public void DuplicateStandaloneRequestIdFields_KeepOnlyFirstValue()
        {
            var message = Logger.NormalizeForDisplay(
                "[预览][调试] VLC预览恢复尝试：RequestId=REQ-1 " +
                "request_id=REQ-1 Attempt=1");

            Assert.AreEqual(1, CountOccurrences(message, "RequestId="));
            StringAssert.Contains(message, "RequestId=REQ-1");
            Assert.IsFalse(message.Contains("request_id="));
        }

        [TestMethod]
        public void LocalProductionSuccess_OmitsMissingRequestIdPlaceholder()
        {
            var message = Logger.NormalizeForDisplay(
                "[预览][信息] 本地预览已启动：Operation=StartCameraPreview " +
                "RequestId=<无> Result=Success DurationMs=12");

            Assert.AreEqual("[预览][信息] 本地预览已启动", message);
            Assert.IsFalse(message.Contains("RequestId=<无>"));
            Assert.IsFalse(message.Contains("DurationMs="));
        }

        [TestMethod]
        public void LatestFrameDiagnostic_IsDebugOnlyAndBoundaryErrorIsConcise()
        {
            var diagnostic = Logger.NormalizeForDisplay(
                "[车牌抓帧][调试] LatestFrameDiagnostic RouteMatched=true " +
                "PreviewRequestId=PRE-1 SessionFound=false PlayerState=stopped " +
                "Generation=7 CacheKey=PlateCJ_External Stage=Lookup " +
                "Error=frame_not_ready");
            var boundaryError = Logger.NormalizeForDisplay(
                "[车牌抓帧][错误] 车牌CJ最新帧获取失败：Operation=SaveLatestPlateFrame " +
                "RequestId=CAP-1 CaptureRequestId=CAP-1 PreviewRequestId=PRE-1 " +
                "Result=Failed ErrorCode=frame_not_ready DurationMs=80 " +
                "Stage=GetLatestFrame PlayerState=stopped");

            StringAssert.Contains(diagnostic, "LatestFrameDiagnostic");
            StringAssert.Contains(diagnostic, "Generation=7");
            Assert.AreEqual(
                "[车牌抓帧][错误] 车牌CJ最新帧获取失败：RequestId=CAP-1 ErrorCode=frame_not_ready",
                boundaryError);
            Assert.IsFalse(boundaryError.Contains("LatestFrameDiagnostic"));
            Assert.IsFalse(boundaryError.Contains("CaptureRequestId="));
            Assert.IsFalse(boundaryError.Contains("PreviewRequestId="));
            Assert.IsFalse(boundaryError.Contains("Generation="));
        }

        [TestMethod]
        public void VlcPlateFrameRecovery_IsAssignedToPreviewModule()
        {
            var message = Logger.NormalizeForDisplay(
                "VLC车牌最新帧已恢复：尺寸=1920x1080");

            Assert.AreEqual("[预览][信息] VLC车牌最新帧已恢复：尺寸=1920x1080", message);
        }

        [TestMethod]
        public void DebugRetainsTechnicalFieldsAndCanonicalOperation()
        {
            var message = Logger.NormalizeForDisplay(
                "[接口][调试] Operation=HZCYKJTHardWare_SaveLatestPlateFrame " +
                "RequestId=CAP-1 CaptureRequestId=CAP-1 PreviewRequestId=PRE-1 " +
                "Result=Success ErrorCode=none DurationMs=32 Stage=GetLatestFrame " +
                "Bytes=2048 Width=1920 Height=1080 FrameAgeMs=20 Source=Cache");

            StringAssert.Contains(message, "[车牌抓帧][调试]");
            StringAssert.Contains(message, "Operation=SaveLatestPlateFrame");
            StringAssert.Contains(message, "CaptureRequestId=CAP-1");
            StringAssert.Contains(message, "PreviewRequestId=PRE-1");
            StringAssert.Contains(message, "DurationMs=32");
            StringAssert.Contains(message, "Stage=GetLatestFrame");
            StringAssert.Contains(message, "Bytes=2048");
            StringAssert.Contains(message, "Width=1920");
            StringAssert.Contains(message, "Height=1080");
            StringAssert.Contains(message, "FrameAgeMs=20");
            StringAssert.Contains(message, "Source=Cache");
        }

        [TestMethod]
        public void ProductionInfo_DoesNotDuplicateCaptureRequestId()
        {
            var message = Logger.NormalizeForDisplay(
                "[车牌抓帧][信息] 车牌CJ图片保存成功：Operation=SaveLatestPlateFrame " +
                "RequestId=CAP-1 CaptureRequestId=CAP-1 PreviewRequestId=PRE-1 " +
                "Result=Success DurationMs=32");

            Assert.AreEqual(
                "[车牌抓帧][信息] 车牌CJ图片保存成功：RequestId=CAP-1",
                message);
            Assert.AreEqual(1, CountOccurrences(message, "RequestId=CAP-1"));
            Assert.IsFalse(message.Contains("CaptureRequestId="));
            Assert.IsFalse(message.Contains("PreviewRequestId="));
        }

        [TestMethod]
        public void RecoveryAggregateRetainsEpisodeCountDurationAndErrorCode()
        {
            var message = Logger.NormalizeForDisplay(
                "[预览][错误] 摄像头预览恢复失败：Operation=RecoverCameraPreview " +
                "RequestId=PRE-1 RecoveryEpisodeId=7 Attempts=5 Result=Failed " +
                "ErrorCode=recovery_exhausted DurationMs=12000");

            StringAssert.Contains(message, "RecoveryEpisodeId=7");
            StringAssert.Contains(message, "Attempts=5");
            StringAssert.Contains(message, "ErrorCode=recovery_exhausted");
            StringAssert.Contains(message, "DurationMs=12000");
        }

        [TestMethod]
        public void TelemetryMessage_RetainsLongRunFields()
        {
            var message = Logger.NormalizeForDisplay(
                "[日志管理][信息] 长稳遥测：Telemetry PrivateBytes=128 " +
                "WorkingSet=256 Threads=12 Handles=88 LogQueue=3");

            StringAssert.Contains(message, "Telemetry");
            StringAssert.Contains(message, "PrivateBytes=128");
            StringAssert.Contains(message, "WorkingSet=256");
            StringAssert.Contains(message, "Threads=12");
            StringAssert.Contains(message, "Handles=88");
            StringAssert.Contains(message, "LogQueue=3");
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
