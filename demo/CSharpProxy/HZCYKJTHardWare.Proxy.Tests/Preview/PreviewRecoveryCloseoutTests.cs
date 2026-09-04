using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Terminal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class PreviewRecoveryCloseoutTests
    {
        [TestMethod]
        public async Task InitialStartOffline_RetainsDesiredSessionUntilStopped()
        {
            var terminalPort = GetUnusedPort();
            using (var client = new TerminalClient())
            using (var host = new Form())
            using (var manager = new PreviewManager(client))
            {
                var started = await manager.StartPreview(
                    PreviewResourceType.Camera, PreviewSessionType.External,
                    host.Handle, "http://127.0.0.1:" + terminalPort,
                    requestId: "initial-offline").ConfigureAwait(false);

                Assert.IsFalse(started);
                Assert.AreEqual(1, manager.ActiveSessionCount,
                    "瞬时网络失败不能删除 Desired Running 会话");
                Assert.IsTrue(await WaitUntilAsync(
                    () => manager.ActiveRecoveryCount > 0, 2000).ConfigureAwait(false));
                Assert.AreEqual(ExternalPreviewStartupState.Recovering,
                    manager.GetExternalPreviewStartupState(
                        PreviewResourceType.Camera, PreviewSessionType.External,
                        "initial-offline"));
                Assert.IsFalse(manager.IsPreviewRunning(
                    PreviewResourceType.Camera, PreviewSessionType.External));

                Assert.IsTrue(await manager.StopPreviewAsync(
                    PreviewResourceType.Camera, PreviewSessionType.External)
                    .ConfigureAwait(false));
                Assert.AreEqual(0, manager.ActiveSessionCount);
                Assert.AreEqual(0, manager.ActiveRecoveryCount);
                Assert.AreEqual(ExternalPreviewStartupState.TerminalFailure,
                    manager.GetExternalPreviewStartupState(
                        PreviewResourceType.Camera, PreviewSessionType.External,
                        "initial-offline"));
            }
        }

        [TestMethod]
        public async Task InitialStartInvalidHwnd_DoesNotCreateSessionOrRecovery()
        {
            using (var client = new TerminalClient())
            using (var manager = new PreviewManager(client))
            {
                var started = await manager.StartPreview(
                    PreviewResourceType.Camera, PreviewSessionType.External,
                    IntPtr.Zero, "http://127.0.0.1:1",
                    requestId: "invalid-hwnd").ConfigureAwait(false);

                Assert.IsFalse(started);
                Assert.AreEqual(0, manager.ActiveSessionCount);
                Assert.AreEqual(0, manager.ActiveRecoveryCount);
            }
        }

        [TestMethod]
        public void OldRecoverySessionIdentityCannotMatchReplacement()
        {
            var oldSession = new PreviewSession { Generation = 11 };
            var replacement = new PreviewSession { Generation = 12 };

            Assert.IsTrue(PreviewManager.IsRecoverySessionCurrent(
                oldSession, oldSession, 11));
            Assert.IsFalse(PreviewManager.IsRecoverySessionCurrent(
                replacement, oldSession, 11));
            Assert.IsFalse(PreviewManager.IsRecoverySessionCurrent(
                oldSession, replacement, 12));
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition,
            int timeoutMs)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (condition())
                    return true;
                await Task.Delay(20).ConfigureAwait(false);
            }
            return condition();
        }

        private static int GetUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
