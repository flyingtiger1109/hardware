using HZCYKJTHardWare.Proxy.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        public void CurrentBindingsForTwoTerminals_RemainIndependent()
        {
            var terminalA = _registry.Prepare(1, "http://192.168.20.30:9098",
                "PROCESS_A", @"C:\capture-a", 0);
            Assert.IsNotNull(terminalA);
            Assert.IsTrue(_registry.Commit(terminalA));

            var terminalB = _registry.Prepare(2, "http://192.168.20.31:9098",
                "PROCESS_B", @"C:\capture-b", 1);
            Assert.IsNotNull(terminalB);
            Assert.IsTrue(_registry.Commit(terminalB));

            Assert.IsTrue(_registry.TryGetCurrent(1, out var currentA));
            Assert.AreEqual("PROCESS_A", currentA.ProcessRequestId);
            Assert.IsTrue(_registry.TryGetCurrent(2, out var currentB));
            Assert.AreEqual("PROCESS_B", currentB.ProcessRequestId);
            Assert.AreEqual(2, _registry.CurrentCount);
            Assert.AreEqual(2, _registry.BindingCount);
        }

        [TestMethod]
        public void ReplacingCurrentBinding_RetainsPreviousRequestRoute()
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

            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_A1", out var retainedA1));
            Assert.AreEqual(TerminalProcessState.Retained, retainedA1.State);
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_A2", out _));
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_B", out _));
            Assert.AreEqual(@"C:\a2", _registry.GetCurrentSaveDir(1));
            Assert.AreEqual(@"C:\b", _registry.GetCurrentSaveDir(2));
        }

        [TestMethod]
        public void CallbackRoute_IsAvailableBeforeStartResponseIsConfirmed()
        {
            _registry.Prepare(1, "http://terminal-a",
                "PROCESS_FAST", @"C:\fast", 0);

            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_FAST", out var session));
            Assert.AreEqual(TerminalProcessState.Registering, session.State);
            Assert.IsTrue(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"fast\"}", out _));
        }

        [TestMethod]
        public void PersistentEvents_GetUniqueDeliveryIds_AndSuppressImmediateDuplicate()
        {
            var registration = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A", @"C:\a", 0);
            Assert.IsTrue(_registry.Commit(registration));
            Assert.IsTrue(_registry.TryGetCurrent(1, out var session));

            Assert.IsTrue(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"001\"}",
                out var firstDeliveryId));
            Assert.IsFalse(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"001\"}", out _));
            Assert.IsTrue(_registry.TryReserveEvent(session,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"002\"}",
                out var secondDeliveryId));

            Assert.AreNotEqual(firstDeliveryId, secondDeliveryId);
            StringAssert.StartsWith(firstDeliveryId, "PROCESS_A_EVENT_");
        }

        [TestMethod]
        public void UnconfirmedStart_RemainsRoutable_WithoutReplacingCurrentBinding()
        {
            var original = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A1", @"C:\a1", 0);
            Assert.IsTrue(_registry.Commit(original));

            var unconfirmed = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A2", @"C:\a2", 1);
            Assert.IsNotNull(unconfirmed);
            _registry.RetainUnconfirmed(unconfirmed);

            Assert.IsTrue(_registry.TryGetCurrent(1, out var current));
            Assert.AreEqual("PROCESS_A1", current.ProcessRequestId);
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_A2", out var retained));
            Assert.AreEqual(TerminalProcessState.Retained, retained.State);
            Assert.IsTrue(_registry.TryReserveEvent(retained,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"late\"}", out _));
        }

        [TestMethod]
        public void EndAcknowledged_ClearsCurrentMetadata_ButKeepsCallbackRoute()
        {
            var start = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A", @"C:\a", 0);
            Assert.IsTrue(_registry.Commit(start));

            _registry.RecordEndAcknowledged(1);

            Assert.IsFalse(_registry.TryGetCurrent(1, out _));
            Assert.AreEqual("", _registry.GetCurrentSaveDir(1));
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_A", out var retained));
            Assert.AreEqual(TerminalProcessState.Retained, retained.State);
            Assert.IsTrue(_registry.TryReserveEvent(retained,
                ProxyResourceTypes.NfcCard, "{\"card_text\":\"in-flight\"}", out _));
        }

        [TestMethod]
        public void EndAcknowledgedForOneTerminal_DoesNotChangeOtherTerminal()
        {
            var terminalA = _registry.Prepare(1, "http://terminal-a",
                "PROCESS_A", @"C:\a", 0);
            var terminalB = _registry.Prepare(2, "http://terminal-b",
                "PROCESS_B", @"C:\b", 0);
            Assert.IsTrue(_registry.Commit(terminalA));
            Assert.IsTrue(_registry.Commit(terminalB));

            _registry.RecordEndAcknowledged(1);

            Assert.IsFalse(_registry.TryGetCurrent(1, out _));
            Assert.IsTrue(_registry.TryGetByRequestId("PROCESS_A", out _));
            Assert.IsTrue(_registry.TryGetCurrent(2, out var currentB));
            Assert.AreEqual("PROCESS_B", currentB.ProcessRequestId);
        }
    }
}
