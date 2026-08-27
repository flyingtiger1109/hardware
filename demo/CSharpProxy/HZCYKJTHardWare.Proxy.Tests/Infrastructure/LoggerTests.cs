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
    }
}
