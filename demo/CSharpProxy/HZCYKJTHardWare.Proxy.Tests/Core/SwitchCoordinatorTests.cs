using System.Threading.Tasks;
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
                Assert.AreEqual(1, oldEpoch.Route.TerminalIndex);
                Assert.IsFalse(oldEpoch.IsCancellationRequested);

                Assert.IsTrue(await coordinator.SwitchToAsync(2));

                Assert.IsTrue(oldEpoch.IsCancellationRequested,
                    "switch must cancel operations admitted on the old route");
                Assert.IsTrue(coordinator.TryCaptureRoute(out var newEpoch));
                Assert.AreEqual(2, newEpoch.Route.TerminalIndex);
                Assert.IsTrue(newEpoch.Generation > oldEpoch.Generation);
                Assert.IsFalse(newEpoch.IsCancellationRequested);
            }
        }
    }
}
