using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class ProxyInstanceIdentityTests
    {
        [TestMethod]
        public void CreateProxyInstanceId_ReturnsUniqueFixedLengthIds()
        {
            var first = DllCommandHandler.CreateProxyInstanceId();
            var second = DllCommandHandler.CreateProxyInstanceId();

            Assert.AreEqual(32, first.Length);
            Assert.AreEqual(32, second.Length);
            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void BuildPingResponse_PreservesStatusAndInstanceId()
        {
            const string instanceId = "0123456789abcdef0123456789abcdef";
            var response = DllCommandHandler.BuildPingResponse(instanceId);

            Assert.AreEqual("ok", JsonHelper.ExtractString(response, "status"));
            Assert.AreEqual(instanceId,
                JsonHelper.ExtractString(response, "proxy_instance_id"));
        }
    }
}
