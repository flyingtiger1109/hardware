using HZCYKJTHardWare.Proxy.Server.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Runtime
{
    [TestClass]
    public class RuntimeMetricsReporterTests
    {
        [TestMethod]
        public void BuildSnapshot_ContainsLongRunResourceIndicators()
        {
            using (var reporter = new RuntimeMetricsReporter(
                null, null, null, null, null))
            {
                var snapshot = reporter.BuildSnapshot();

                StringAssert.Contains(snapshot, "private_mb=");
                StringAssert.Contains(snapshot, "threads=");
                StringAssert.Contains(snapshot, "handles=");
                StringAssert.Contains(snapshot, "gdi_handles=");
                StringAssert.Contains(snapshot, "gc2=");
                StringAssert.Contains(snapshot, "disk_free_mb=");
            }
        }
    }
}
