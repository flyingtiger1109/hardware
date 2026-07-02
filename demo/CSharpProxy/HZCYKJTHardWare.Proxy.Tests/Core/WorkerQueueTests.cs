using System;
using System.Collections.Concurrent;
using System.Linq;
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
        public void Enqueue_ReplaceMode_ExecutesFirstAndLatestOnly()
        {
            var completed = new ConcurrentBag<int>();
            using (var firstStarted = new ManualResetEventSlim(false))
            using (var releaseFirst = new ManualResetEventSlim(false))
            using (var latestCompleted = new ManualResetEventSlim(false))
            using (var queue = new WorkerQueue<int>("replace_exact", 2, task =>
            {
                if (task.Data == 1)
                {
                    firstStarted.Set();
                    releaseFirst.Wait(3000);
                }
                completed.Add(task.Data);
                if (task.Data == 3) latestCompleted.Set();
            }, replaceOld: true, timeoutMs: 5000))
            {
                Assert.IsTrue(queue.Enqueue(1, 0));
                Assert.IsTrue(firstStarted.Wait(1000), "first task must be executing");
                Assert.IsTrue(queue.Enqueue(2, 0), "second task becomes pending");
                Assert.IsTrue(queue.Enqueue(3, 0), "third task replaces pending task");

                releaseFirst.Set();
                Assert.IsTrue(latestCompleted.Wait(3000), "latest task must execute");
                Assert.IsTrue(completed.Contains(1));
                Assert.IsTrue(completed.Contains(3));
                Assert.IsFalse(completed.Contains(2), "replaced task must never execute");
                Assert.AreEqual(1L, queue.Replaced);
            }
        }

        [TestMethod]
        public void PendingTask_CompletedByCaller_IsNotExecutedLater()
        {
            var executed = new ConcurrentBag<int>();
            using (var firstStarted = new ManualResetEventSlim(false))
            using (var releaseFirst = new ManualResetEventSlim(false))
            using (var queue = new WorkerQueue<TestQueueItem>("late_skip", 2, task =>
            {
                if (task.Data.Id == 1)
                {
                    firstStarted.Set();
                    releaseFirst.Wait(3000);
                }
                executed.Add(task.Data.Id);
            }, replaceOld: true, timeoutMs: 5000))
            {
                var first = new TestQueueItem(1);
                var pending = new TestQueueItem(2);
                Assert.IsTrue(queue.Enqueue(first, 0));
                Assert.IsTrue(firstStarted.Wait(1000));
                Assert.IsTrue(queue.Enqueue(pending, 0));

                pending.TrySetQueueResult("{\"error\":true,\"code\":\"timeout\"}");
                releaseFirst.Set();
                Thread.Sleep(300);

                Assert.IsTrue(executed.Contains(1));
                Assert.IsFalse(executed.Contains(2));
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

        private sealed class TestQueueItem : IQueueResultSink
        {
            private readonly TaskCompletionSource<string> _completion =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal TestQueueItem(int id) { Id = id; }
            internal int Id { get; }
            public bool IsQueueResultCompleted => _completion.Task.IsCompleted;
            public void TrySetQueueResult(string result) { _completion.TrySetResult(result); }
        }
    }
}
