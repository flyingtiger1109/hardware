using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Server.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Runtime
{
    [TestClass]
    public class ActiveTasksTrackerTests
    {
        [TestMethod]
        public async Task TryRun_RejectsWorkBeyondCapacity()
        {
            using (var tracker = new ActiveTasksTracker(1, 2000))
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                Assert.IsTrue(tracker.TryRun(() =>
                {
                    entered.Set();
                    release.Wait(2000);
                }, "first"));
                Assert.IsTrue(entered.Wait(1000));

                Assert.IsFalse(tracker.TryRun(() => { }, "second"));
                Assert.AreEqual(1, tracker.ActiveCount);

                release.Set();
                await tracker.WaitAllAsync(2000);
                Assert.AreEqual(0, tracker.ActiveCount);
            }
        }

        [TestMethod]
        public async Task TryRun_QuietRejectionCanBeRetried()
        {
            using (var tracker = new ActiveTasksTracker(1, 2000))
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                Assert.IsTrue(tracker.TryRun(() =>
                {
                    entered.Set();
                    release.Wait(2000);
                }, "recovery_blocker"));
                Assert.IsTrue(entered.Wait(1000));

                Assert.IsFalse(tracker.TryRun(() => Task.CompletedTask,
                    "preview_recovery", logRejection: false));
                Assert.AreEqual(1, tracker.TotalRejected);

                release.Set();
                await tracker.WaitAllAsync(2000);

                Assert.IsTrue(tracker.TryRun(() => Task.CompletedTask,
                    "preview_recovery", logRejection: false));
                await tracker.WaitAllAsync(2000);
                Assert.AreEqual(0, tracker.ActiveCount);
            }
        }

        [TestMethod]
        public void Dispose_PreventsNewWork()
        {
            var tracker = new ActiveTasksTracker(1, 1000);
            tracker.Dispose();

            Assert.IsFalse(tracker.TryRun(() => { }, "after_dispose"));
            Assert.AreEqual(0, tracker.ActiveCount);
        }
    }
}
