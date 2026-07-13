using HZCYKJTHardWare.Proxy.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Infrastructure
{
    [TestClass]
    public class OperationTimeoutsTests
    {
        [TestMethod]
        public void TimeoutBudget_KeepsInnerRequestsShorterThanProxyWaits()
        {
            Assert.IsTrue(OperationTimeouts.FaceTerminalRequestMs <
                          OperationTimeouts.CaptureProxyWaitMs);
            Assert.IsTrue(OperationTimeouts.FingerprintTerminalRequestMs <
                          OperationTimeouts.CaptureProxyWaitMs);
            Assert.IsTrue(OperationTimeouts.AsyncTerminalRequestMs <
                          OperationTimeouts.AsyncProxyWaitMs);
            Assert.IsTrue(OperationTimeouts.AuthorizeTerminalRequestMs <
                          OperationTimeouts.AuthorizeProxyWaitMs);
        }
    }
}
