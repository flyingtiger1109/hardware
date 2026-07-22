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
    }
}
