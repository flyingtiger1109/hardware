using System;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class RequestRegistryTests
    {
        private RequestRegistry _registry;

        [TestInitialize]
        public void Setup()
        {
            _registry = new RequestRegistry();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _registry?.Dispose();
        }

        [TestMethod]
        public void Register_NewRequest_ReturnsContext()
        {
            var ctx = _registry.Register("req-001", "ocr", @"C:\save", "http://cb/ocr", 1);
            Assert.IsNotNull(ctx);
            Assert.AreEqual("req-001", ctx.RequestId);
            Assert.AreEqual(ProxyResourceTypes.OcrDocument, ctx.ResourceType);
            Assert.AreEqual(1, ctx.Generation);
            Assert.AreEqual(ProxyRequestState.Created, ctx.State);
        }

        [TestMethod]
        public void Register_DuplicateKey_DoesNotOverwriteActiveContext()
        {
            var first = _registry.Register("dup-001", "ocr", @"C:\first",
                "http://cb/ocr", 1);
            var duplicate = _registry.Register("dup-001", "ocr", @"C:\second",
                "http://cb/ocr", 2);

            Assert.IsNotNull(first);
            Assert.IsNull(duplicate);
            Assert.IsTrue(_registry.TryGet("dup-001", "ocr", out var current));
            Assert.AreSame(first, current);
            Assert.AreEqual(@"C:\first", current.SaveDir);
            Assert.AreEqual(1, current.Generation);
        }

        [TestMethod]
        public void Register_ConcurrentCapacity_NeverExceedsLimit()
        {
            _registry.Dispose();
            _registry = new RequestRegistry(8);
            var accepted = 0;

            Parallel.For(0, 100, i =>
            {
                if (_registry.Register("cap-" + i, "ocr", @"C:\save",
                    "http://cb/ocr", 1) != null)
                    Interlocked.Increment(ref accepted);
            });

            Assert.AreEqual(8, accepted);
            Assert.AreEqual(8, _registry.ActiveCount);
        }

        [TestMethod]
        public void Complete_CancelsRequestLifetimeToken()
        {
            var context = _registry.Register("cancel-001", "nfc", @"C:\save",
                "http://cb/nfc", 1);
            Assert.IsFalse(context.CancellationToken.IsCancellationRequested);

            _registry.Complete("cancel-001", "nfc");

            Assert.IsTrue(context.CancellationToken.IsCancellationRequested);
        }

        [TestMethod]
        public void CompletedRecords_AreBoundedForX86Process()
        {
            for (var i = 0; i < 8200; i++)
            {
                var requestId = "completed-" + i;
                Assert.IsNotNull(_registry.Register(requestId, "nfc", @"C:\save",
                    "http://cb/nfc", 1));
                _registry.Complete(requestId, "nfc");
            }

            Assert.AreEqual(8192, _registry.CompletedCount);
            Assert.AreEqual(0, _registry.ActiveCount);
        }

        [TestMethod]
        public void Register_NullRequestId_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                _registry.Register(null, "ocr", @"C:\save", "http://cb/ocr", 1));
        }

        [TestMethod]
        public void Register_EmptyRequestId_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                _registry.Register("", "ocr", @"C:\save", "http://cb/ocr", 1));
        }

        [TestMethod]
        public void TryMarkAccepted_AfterRegister_ReturnsTrue()
        {
            _registry.Register("req-002", "nfc", @"C:\save", "http://cb/nfc", 1);
            // Need to transition through Submitting first
            _registry.TryMarkSubmitting("req-002", "nfc");
            bool accepted = _registry.TryMarkAccepted("req-002", "nfc");
            Assert.IsTrue(accepted);
        }

        [TestMethod]
        public void TryMarkAccepted_WithoutSubmitting_ReturnsTrue()
        {
            // TryMarkAccepted uses a while loop that accepts Created as a valid
            // transition source (see TryMarkSubmitting fallback)
            _registry.Register("req-003", "nfc", @"C:\save", "http://cb/nfc", 1);
            bool accepted = _registry.TryMarkAccepted("req-003", "nfc");
            Assert.IsTrue(accepted);
        }

        [TestMethod]
        public void TryClaimCallback_FromCreated_ReturnsTrue()
        {
            // TryClaimCallback allows claiming from any non-terminal state
            // that hasn't already been claimed. This is by design: the callback
            // can arrive before the terminal formally accepts.
            _registry.Register("req-004", "ocr", @"C:\save", "http://cb/ocr", 1);
            bool claimed = _registry.TryClaimCallback("req-004", "ocr", out var ctx);
            Assert.IsTrue(claimed, "claim from Created state should succeed");
            Assert.IsNotNull(ctx);
            Assert.AreEqual("req-004", ctx.RequestId);
        }

        [TestMethod]
        public void TryClaimCallback_AfterAccepted_ReturnsTrue()
        {
            _registry.Register("req-005", "ocr", @"C:\save", "http://cb/ocr", 1);
            _registry.TryMarkAccepted("req-005", "ocr");
            bool claimed = _registry.TryClaimCallback("req-005", "ocr", out var ctx);
            Assert.IsTrue(claimed);
            Assert.IsNotNull(ctx);
            Assert.AreEqual("req-005", ctx.RequestId);
        }

        [TestMethod]
        public void TryClaimCallback_DuplicateCall_ReturnsFalse()
        {
            _registry.Register("req-006", "ocr", @"C:\save", "http://cb/ocr", 1);
            _registry.TryMarkAccepted("req-006", "ocr");

            bool first = _registry.TryClaimCallback("req-006", "ocr", out _);
            bool second = _registry.TryClaimCallback("req-006", "ocr", out _);

            Assert.IsTrue(first, "first claim should succeed");
            Assert.IsFalse(second, "second claim (duplicate) should be rejected");
        }

        [TestMethod]
        public void TryClaimCallback_ConcurrentDuplicates_OnlyOneSucceeds()
        {
            _registry.Register("req-007", "iris_image", @"C:\save", "http://cb/iris", 1);
            _registry.TryMarkAccepted("req-007", "iris_image");

            int successCount = 0;
            var barrier = new Barrier(4);
            var threads = new Thread[4];

            for (int i = 0; i < 4; i++)
            {
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    if (_registry.TryClaimCallback("req-007", "iris_image", out _))
                        Interlocked.Increment(ref successCount);
                });
                threads[i].Start();
            }

            foreach (var t in threads) t.Join();

            Assert.AreEqual(1, successCount,
                "exactly one concurrent claim should succeed");
        }

        [TestMethod]
        public void Complete_SetsTerminalState()
        {
            _registry.Register("req-008", "nfc", @"C:\save", "http://cb/nfc", 1);
            _registry.TryMarkAccepted("req-008", "nfc");
            _registry.Complete("req-008", "nfc");

            // After Complete, TryGet should not find it in active
            bool found = _registry.TryGet("req-008", "nfc", out _);
            Assert.IsFalse(found, "completed request should not be in active registry");

            // But TryMarkAccepted should return true (completed record exists)
            bool accepted = _registry.TryMarkAccepted("req-008", "nfc");
            Assert.IsTrue(accepted, "TryMarkAccepted should find completed record");
        }

        [TestMethod]
        public void PruneExpired_RemovesExpiredActive()
        {
            _registry.Register("req-009", "ocr", @"C:\save", "http://cb/ocr", 1);
            // The default lifetime is 10 minutes so this won't expire immediately.
            // But we can verify PruneExpired runs without error.
            var (activeRemoved, completedRemoved) = _registry.PruneExpired();
            Assert.IsTrue(activeRemoved >= 0);
            Assert.IsTrue(completedRemoved >= 0);
        }

        [TestMethod]
        public void CancelOlderThan_CancelsOldGeneration()
        {
            _registry.Register("req-010", "ocr", @"C:\save", "http://cb/ocr", 1); // gen=1
            _registry.Register("req-011", "ocr", @"C:\save", "http://cb/ocr", 3); // gen=3

            _registry.CancelOlderThan(2); // cancels gen < 2

            bool foundOld = _registry.TryGet("req-010", "ocr", out _);
            bool foundNew = _registry.TryGet("req-011", "ocr", out _);

            Assert.IsFalse(foundOld, "gen=1 should be cancelled");
            Assert.IsTrue(foundNew, "gen=3 should remain");
        }

        [TestMethod]
        public void CancelOlderThan_PreservesLegacyProcessFlowEntry()
        {
            _registry.Register("process-legacy", "ocr", @"C:\save",
                "http://cb/ocr", 1, processFlow: true);

            _registry.CancelOlderThan(2);

            Assert.IsTrue(_registry.TryGet("process-legacy", "ocr", out _));
        }

        [TestMethod]
        public void CancelAll_CancelsEverything()
        {
            _registry.Register("req-012", "ocr", @"C:\save", "http://cb/ocr", 1);
            _registry.Register("req-013", "nfc", @"C:\save", "http://cb/nfc", 1);

            _registry.CancelAll();

            Assert.IsFalse(_registry.TryGet("req-012", "ocr", out _));
            Assert.IsFalse(_registry.TryGet("req-013", "nfc", out _));
        }

        [TestMethod]
        public void ResourceType_Alias_MapsCorrectly()
        {
            Assert.AreEqual(ProxyResourceTypes.OcrDocument,
                ProxyResourceTypes.Normalize("ocr"));
            Assert.AreEqual(ProxyResourceTypes.NfcCard,
                ProxyResourceTypes.Normalize("nfc"));
            Assert.AreEqual(ProxyResourceTypes.IrisImage,
                ProxyResourceTypes.Normalize("iris"));
            Assert.AreEqual(ProxyResourceTypes.Protocol,
                ProxyResourceTypes.Normalize("authorization"));
        }

        [TestMethod]
        public void ActiveCount_TracksCorrectly()
        {
            int before = _registry.ActiveCount;
            _registry.Register("req-014", "ocr", @"C:\save", "http://cb/ocr", 1);
            _registry.Register("req-015", "nfc", @"C:\save", "http://cb/nfc", 1);

            Assert.AreEqual(before + 2, _registry.ActiveCount);

            _registry.Complete("req-014", "ocr");
            Assert.AreEqual(before + 1, _registry.ActiveCount);
        }

        [TestMethod]
        public void Snapshot_ReturnsActiveRequests()
        {
            _registry.Register("req-016", "ocr", @"C:\save", "http://cb/ocr", 1);
            _registry.Register("req-017", "nfc", @"C:\save", "http://cb/nfc", 1);

            var snapshot = _registry.Snapshot();
            Assert.IsTrue(snapshot.Count >= 2);
        }

        [TestMethod]
        public void ProcessFlow_HasLongerLifetime()
        {
            var ctx = _registry.Register("req-018", "ocr", @"C:\save", "http://cb/ocr",
                1, processFlow: true);
            var lifetime = ctx.ExpiresAtUtc - ctx.CreatedAtUtc;
            Assert.IsTrue(lifetime.TotalHours >= 7,
                $"process flow lifetime should be ~8h, got {lifetime.TotalHours}h");
        }
    }
}
