using System.Threading.Tasks;
using System.Collections.Generic;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server.Coordinator;
using HZCYKJTHardWare.Proxy.Terminal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class SwitchCoordinatorTests
    {
        [TestMethod]
        public async Task SwitchToAsync_NotifiesCommittedTerminal()
        {
            var terminalManager = new TerminalManager();
            using (var terminalClient = new TerminalClient())
            using (var previewManager = new PreviewManager(terminalClient))
            using (var requestRegistry = new RequestRegistry())
            using (var queueManager = new QueueManager())
            {
                var notifiedIndex = 0;
                var coordinator = new SwitchCoordinator(
                    terminalManager,
                    previewManager,
                    requestRegistry,
                    queueManager,
                    _ => { },
                    index => notifiedIndex = index);

                var switched = await coordinator.SwitchToAsync(2);

                Assert.IsTrue(switched);
                Assert.AreEqual(2, terminalManager.CurrentIndex);
                Assert.AreEqual(2, notifiedIndex);
            }
        }

        [TestMethod]
        public async Task SwitchToAsync_CancelsOldRouteEpochAndPublishesNewSnapshot()
        {
            var terminalManager = new TerminalManager();
            using (var terminalClient = new TerminalClient())
            using (var previewManager = new PreviewManager(terminalClient))
            using (var requestRegistry = new RequestRegistry())
            using (var queueManager = new QueueManager())
            {
                var coordinator = new SwitchCoordinator(
                    terminalManager,
                    previewManager,
                    requestRegistry,
                    queueManager,
                    _ => { });

                Assert.IsTrue(coordinator.TryCaptureRoute(out var oldEpoch));
                Assert.IsFalse(oldEpoch.IsCancellationRequested);
                var oldIndex = oldEpoch.Route.TerminalIndex;
                var newIndex = oldIndex == 1 ? 2 : 1;

                Assert.IsTrue(await coordinator.SwitchToAsync(newIndex));

                Assert.IsTrue(oldEpoch.IsCancellationRequested,
                    "switch must cancel operations admitted on the old route");
                Assert.IsTrue(coordinator.TryCaptureRoute(out var newEpoch));
                Assert.AreEqual(newIndex, newEpoch.Route.TerminalIndex);
                Assert.IsTrue(newEpoch.Generation > oldEpoch.Generation);
                Assert.IsFalse(newEpoch.IsCancellationRequested);
            }
        }

        [TestMethod]
        public async Task SwitchTerminalRequestIdPropagation()
        {
            var messages = new List<string>();
            var terminalManager = new TerminalManager();
            using (var terminalClient = new TerminalClient())
            using (var previewManager = new PreviewManager(terminalClient))
            using (var requestRegistry = new RequestRegistry())
            using (var queueManager = new QueueManager())
            {
                var coordinator = new SwitchCoordinator(
                    terminalManager,
                    previewManager,
                    requestRegistry,
                    queueManager,
                    messages.Add);

                Assert.IsTrue(await coordinator.SwitchToAsync(2, "SWITCH-L8-1"));
                StringAssert.Contains(string.Join("\n", messages),
                    "Operation=SwitchTerminal RequestId=SWITCH-L8-1");
            }
        }
    }
}
