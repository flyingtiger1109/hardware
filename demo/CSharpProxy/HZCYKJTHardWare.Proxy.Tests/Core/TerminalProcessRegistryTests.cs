using Microsoft.VisualStudio.TestTools.UnitTesting;
using HZCYKJTHardWare.Proxy.Core;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class TerminalProcessRegistryTests
    {
        private TerminalProcessRegistry _registry;

        [TestInitialize]
        public void Setup()
        {
            _registry = new TerminalProcessRegistry();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _registry.Dispose();
        }

        [TestMethod]
        public void SessionsForTwoTerminals_RemainIndependentlyActive()
        {
            var terminalA = _registry.Prepare(1, "http://192.168.20.30:9098",
                "PROCESS_A", @"C:\capture-a", 0);
            Assert.IsNotNull(terminalA);
            Assert.IsTrue(_registry.Commit(terminalA));

            var terminalB = _registry.Prepare(2, "http://192.168.20.31:9098",
                "PROCESS_B", @"C:\capture-b", 1);
            Assert.IsNotNull(terminalB);
            Assert.IsTrue(_registry.Commit(terminalB));

            Assert.IsTrue(_registry.TryGetActive(1, out var activeA));
            Assert.AreEqual("PROCESS_A", activeA.ProcessRequestId);
            Assert.IsTrue(_registry.TryGetActive(2, out var activeB));
            Assert.AreEqual("PROCESS_B", activeB.ProcessRequestId);
            Assert.AreEqual(2, _registry.ActiveCount);
        }

        [TestMethod]
        public void ReplacingOneTerminal_DoesNotRemoveOtherTerminalSession()
        {
            var terminalA1 = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A1", @"C:\a1", 0);
            var terminalB = _registry.Prepare(2, "http://terminal-b",
                "PROCESS_B", @"C:\b", 1);
            Assert.IsTrue(_registry.Commit(terminalA1));
            Assert.IsTrue(_registry.Commit(terminalB));

            var terminalA2 = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A2", @"C:\a2", 2);
            Assert.IsTrue(_registry.Commit(terminalA2));

            Assert.IsFalse(_registry.TryGetByRequestId("PROCESS_A1", out _));
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_A2", out _));
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_B", out _));
            Assert.AreEqual(@"C:\b", _registry.GetActiveSaveDir(2));
        }

        [TestMethod]
        public void PersistentEvents_GetUniqueDeliveryIds_AndSuppressImmediateDuplicate()
        {
            var registration = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A", @"C:\a", 0);
            Assert.IsTrue(_registry.Commit(registration));
            Assert.IsTrue(_registry.TryGetActive(1, out var session));

            Assert.IsTrue(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"001\"}",
                out var firstDeliveryId));
            Assert.IsFalse(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"001\"}",
                out _));
            Assert.IsTrue(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"002\"}",
                out var secondDeliveryId));

            Assert.AreNotEqual(firstDeliveryId, secondDeliveryId);
            StringAssert.StartsWith(firstDeliveryId, "PROCESS_A_EVENT_");
        }

        [TestMethod]
        public void RollbackStart_PreservesPreviouslyActiveSession()
        {
            var original = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A1", @"C:\a1", 0);
            Assert.IsTrue(_registry.Commit(original));

            var replacement = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A2", @"C:\a2", 1);
            Assert.IsNotNull(replacement);
            _registry.Rollback(replacement);

            Assert.IsTrue(_registry.TryGetActive(1, out var active));
            Assert.AreEqual("PROCESS_A1", active.ProcessRequestId);
            Assert.IsFalse(_registry.TryGetByRequestId("PROCESS_A2", out _));
        }
    }
}
