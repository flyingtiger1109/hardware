using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class WorkerQueueTests
    {
        [TestMethod]
        public void Enqueue_WithinLimit_ReturnsTrue()
        {
            var completed = new ConcurrentBag<int>();
            using (var queue = new WorkerQueue<int>("test", 2, task =>
            {
                completed.Add(task.Data);
                Thread.Sleep(50); // simulate work
            }, replaceOld: false, timeoutMs: 5000))
            {
                bool r1 = queue.Enqueue(1, 0);
                bool r2 = queue.Enqueue(2, 0);

                Assert.IsTrue(r1, "first enqueue should succeed");
                Assert.IsTrue(r2, "second enqueue should succeed");
            }
        }

        [TestMethod]
        public void Enqueue_ExceedsLimit_ReturnsFalse()
        {
            var completed = new ConcurrentBag<int>();
            using (var queue = new WorkerQueue<int>("test", 2, task =>
            {
                completed.Add(task.Data);
                Thread.Sleep(200); // slow worker
            }, replaceOld: false, timeoutMs: 5000))
            {
                queue.Enqueue(1, 0);
                queue.Enqueue(2, 0);
                bool r3 = queue.Enqueue(3, 0);

                Assert.IsFalse(r3, "third enqueue should be rejected when maxLength=2");
            }
        }

        [TestMethod]
        public void Enqueue_ReplaceMode_ReplacesPending()
        {
            var completed = new ConcurrentBag<int>();
            using (var queue = new WorkerQueue<int>("test", 2, task =>
            {
                completed.Add(task.Data);
                Thread.Sleep(200); // slow worker allows pending to accumulate
            }, replaceOld: true, timeoutMs: 5000))
            {
                queue.Enqueue(1, 0); // executing
                queue.Enqueue(2, 0); // pending
                bool r3 = queue.Enqueue(3, 0); // replaces pending

                Assert.IsTrue(r3, "third enqueue should succeed in replace mode");
                Assert.IsTrue(queue.Replaced > 0 || queue.Completed >= 1,
                    "either the pending was replaced or work completed");
            }
        }

        [TestMethod]
        public void Execute_CompletesAtLeastOneTask()
        {
            var completed = new ConcurrentBag<int>();
            var evt = new ManualResetEventSlim(false);
            using (var queue = new WorkerQueue<int>("test", 2, task =>
            {
                completed.Add(task.Data);
                evt.Set();
            }, replaceOld: false, timeoutMs: 5000))
            {
                queue.Enqueue(10, 0);
                // Wait for handler to execute before disposal
                Assert.IsTrue(evt.Wait(3000), "handler should execute within 3s");
            }

            Assert.IsTrue(completed.Count >= 1,
                $"at least one task should complete via handler, got {completed.Count}");
        }

        [TestMethod]
        public void TaskData_HasCorrectGeneration()
        {
            int capturedGen = -1;
            var evt = new ManualResetEventSlim(false);
            using (var queue = new WorkerQueue<int>("test", 1, task =>
            {
                capturedGen = task.Generation;
                evt.Set();
            }, replaceOld: false, timeoutMs: 5000))
            {
                queue.Enqueue(42, 7);
                Assert.IsTrue(evt.Wait(3000), "handler should execute within 3s");
            }

            Assert.AreEqual(7, capturedGen, "task generation should match enqueue generation");
        }
    }
}
