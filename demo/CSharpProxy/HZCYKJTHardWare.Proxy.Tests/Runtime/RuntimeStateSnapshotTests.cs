using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Server.Runtime;
using HZCYKJTHardWare.Proxy.Terminal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Runtime
{
    [TestClass]
    public class RuntimeStateSnapshotTests
    {
        [TestMethod]
        public void InitialState_IsUnknownAndDoesNotAssumeOnline()
        {
            var terminalManager = new TerminalManager();
            var tracker = new RuntimeStateTracker(terminalManager);

            var snapshot = tracker.GetSnapshot(null);

            Assert.AreEqual(2, snapshot.Terminals.Count);
            Assert.IsTrue(snapshot.Terminals.All(state => !state.Reachable.HasValue));
            Assert.IsTrue(snapshot.Terminals.All(state => state.FailureCount == 0));
            Assert.IsTrue(snapshot.Terminals.All(state => state.LastSuccessUtc == null));
            Assert.IsTrue(snapshot.Terminals.All(state => state.LastFailureUtc == null));
        }

        [TestMethod]
        public void SuccessFailureRecovery_StoresHistoryAndResetsConsecutiveFailures()
        {
            var tracker = new RuntimeStateTracker(new TerminalManager());

            tracker.RecordRequest(1, Observation("http://192.168.20.30:9098",
                responseReceived: true, requestSucceeded: true, "", 42));
            tracker.RecordRequest(1, Observation("http://192.168.20.30:9098",
                responseReceived: false, requestSucceeded: false, "timeout", 5000));
            tracker.RecordRequest(1, Observation("http://192.168.20.30:9098",
                responseReceived: false, requestSucceeded: false, "network_error", 100));

            var failed = tracker.GetSnapshot(null).Terminals.Single(state =>
                state.TerminalIndex == 1);
            Assert.AreEqual(2, failed.FailureCount);
            Assert.AreEqual(2, failed.ConsecutiveFailures);
            Assert.IsFalse(failed.Reachable.Value);
            Assert.AreEqual("network_error", failed.LastErrorCode);
            Assert.IsNotNull(failed.LastSuccessUtc);
            Assert.IsNotNull(failed.LastFailureUtc);

            tracker.RecordRequest(1, Observation("http://192.168.20.30:9098",
                responseReceived: true, requestSucceeded: true, "", 75));

            var recovered = tracker.GetSnapshot(null).Terminals.Single(state =>
                state.TerminalIndex == 1);
            Assert.AreEqual(2, recovered.FailureCount);
            Assert.AreEqual(0, recovered.ConsecutiveFailures);
            Assert.IsTrue(recovered.Reachable.Value);
            Assert.AreEqual("network_error", recovered.LastErrorCode,
                "恢复后保留最近一次失败原因，便于排查历史故障");
            Assert.IsNotNull(recovered.LastSuccessUtc);
            Assert.IsNotNull(recovered.LastFailureUtc);
        }

        [TestMethod]
        public void TerminalStates_AreIsolatedAndCurrentTerminalIsReadFromManager()
        {
            var terminalManager = new TerminalManager();
            var tracker = new RuntimeStateTracker(terminalManager);

            tracker.RecordRequest(1, Observation("http://t1:9098",
                responseReceived: false, requestSucceeded: false, "timeout", 10));
            tracker.RecordRequest(2, Observation("http://t2:9098",
                responseReceived: true, requestSucceeded: true, "", 20));

            terminalManager.SwitchTo(2);
            var snapshot = tracker.GetSnapshot(null);
            var terminal1 = snapshot.Terminals.Single(state => state.TerminalIndex == 1);
            var terminal2 = snapshot.Terminals.Single(state => state.TerminalIndex == 2);

            Assert.AreEqual(2, snapshot.CurrentTerminalIndex);
            Assert.AreEqual(1, terminal1.FailureCount);
            Assert.AreEqual(1, terminal1.ConsecutiveFailures);
            Assert.AreEqual(0, terminal2.FailureCount);
            Assert.AreEqual(0, terminal2.ConsecutiveFailures);
            Assert.IsTrue(terminal2.Reachable.Value);
        }

        [TestMethod]
        public void HealthObservation_IsCopiedAndKeptPerTerminal()
        {
            var tracker = new RuntimeStateTracker(new TerminalManager());
            var status = new HealthStatus
            {
                TerminalIndex = 1,
                IsHealthy = false,
                ErrorMessage = "设备状态异常",
                Devices = new List<DeviceHealth>
                {
                    new DeviceHealth
                    {
                        Id = "fingerprint",
                        Status = "offline",
                        Message = "silence_timeout",
                        IsOnline = false
                    }
                }
            };

            tracker.ObserveHealth(status);
            status.Devices[0].Status = "online";
            status.Devices[0].IsOnline = true;

            var snapshot = tracker.GetSnapshot(null);
            var terminal = snapshot.Terminals.Single(state => state.TerminalIndex == 1);
            var device = terminal.Devices.Single(item => item.Id == "fingerprint");

            Assert.IsFalse(terminal.HealthHealthy.Value);
            Assert.AreEqual("设备状态异常", terminal.LastHealthError);
            Assert.IsNotNull(terminal.LastHealthObservedUtc);
            Assert.AreEqual("offline", device.Status);
            Assert.IsFalse(device.IsOnline);
        }

        [TestMethod]
        public void ConcurrentSnapshotReads_DoNotThrowOrCrossContaminateState()
        {
            var tracker = new RuntimeStateTracker(new TerminalManager());
            var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                for (var i = 0; i < 500; i++)
                {
                    if ((i + worker) % 7 == 0)
                    {
                        tracker.RecordRequest(1, Observation("http://t1:9098",
                            responseReceived: false, requestSucceeded: false,
                            "network_error", i));
                    }

                    var snapshot = tracker.GetSnapshot(null);
                    Assert.AreEqual(2, snapshot.Terminals.Count);
                    Assert.IsTrue(snapshot.Terminals.All(state =>
                        state.TerminalIndex == 1 || state.TerminalIndex == 2));
                }
            })).ToArray();

            Task.WaitAll(tasks);
        }

        private static TerminalRequestObservation Observation(string baseUrl,
            bool responseReceived, bool requestSucceeded, string errorCode,
            long elapsedMs)
        {
            return new TerminalRequestObservation(baseUrl, responseReceived,
                requestSucceeded, errorCode, elapsedMs, ignored: false);
        }

    }
}
