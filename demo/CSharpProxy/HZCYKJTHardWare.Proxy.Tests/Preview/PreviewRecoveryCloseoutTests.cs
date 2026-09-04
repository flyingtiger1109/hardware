using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server;
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
        public async Task InitialStartOffline_CameraUrlFailureHasSinglePreviewWarning()
        {
            await AssertInitialStartOfflineUrlFailureLogAsync(
                PreviewResourceType.Camera).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task InitialStartOffline_FingerprintUrlFailureHasSinglePreviewWarning()
        {
            await AssertInitialStartOfflineUrlFailureLogAsync(
                PreviewResourceType.Fingerprint).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task InitialStartOffline_IrisUrlFailureHasSinglePreviewWarning()
        {
            await AssertInitialStartOfflineUrlFailureLogAsync(
                PreviewResourceType.Iris).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ExplicitPreviewUrlRequestFailure_RetainsProductionWarningWithoutRecovery()
        {
            var terminalPort = GetUnusedPort();
            var requestId = "explicit-url-failure-" + Guid.NewGuid().ToString("N");
            Logger.SetMinLevel("debug");
            try
            {
                using (var client = new TerminalClient())
                using (var manager = new PreviewManager(client))
                {
                    Logger.SetMinLevel("debug");
                    var route = new TerminalRouteSnapshot(
                        1, "test", "http://127.0.0.1:" + terminalPort, 0);
                    var routeEpoch = new TerminalRouteEpochSnapshot(
                        route, 0, CancellationToken.None);
                    var handler = new DllCommandHandler(
                        null, null, null, manager, null, null, null,
                        message => Logger.WriteMessage(message), null,
                        null, null, null, null);
                    var method = typeof(DllCommandHandler).GetMethod(
                        "HandlePreviewUrl",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(method);

                    var response = await ((Task<string>)method.Invoke(
                        handler, new object[] {
                            PreviewResourceType.Camera, routeEpoch, requestId
                        })).ConfigureAwait(false);

                    StringAssert.Contains(response, "preview_url_failed");
                    Assert.AreEqual(0, manager.ActiveRecoveryCount);
                    Logger.Flush(5000);

                    var lines = ReadLogLinesForRequest(requestId);
                    var previewWarnings = lines
                        .Where(line => line.Contains("[预览][警告]"))
                        .ToArray();
                    var terminalProductionFailures = lines
                        .Where(line => line.Contains("[终端通信][警告]") ||
                                       line.Contains("[终端通信][错误]"))
                        .ToArray();
                    var terminalFailureLines = lines
                        .Where(line => line.Contains("POST网络错误") ||
                                       line.Contains("POST超时") ||
                                       line.Contains("POST失败：") ||
                                       line.Contains("POST异常："))
                        .ToArray();
                    var productionFailureLines = lines
                        .Where(line => line.Contains("[预览][信息]") ||
                                       line.Contains("[预览][警告]") ||
                                       line.Contains("[预览][错误]") ||
                                       line.Contains("[终端通信][信息]") ||
                                       line.Contains("[终端通信][警告]") ||
                                       line.Contains("[终端通信][错误]"))
                        .ToArray();
                    Assert.AreEqual(1, previewWarnings.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.AreEqual(1, productionFailureLines.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.AreEqual(0, terminalProductionFailures.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.IsTrue(terminalFailureLines.All(line => line.Contains("[调试]")),
                        string.Join(Environment.NewLine, lines));
                    StringAssert.Contains(previewWarnings[0], "预览地址请求失败");
                    Assert.IsFalse(lines.Any(line => line.Contains("正在自动恢复")));
                }
            }
            finally
            {
                Logger.Flush(5000);
                Logger.SetMinLevel("info");
            }
        }

        [TestMethod]
        public async Task TerminalClient_DefaultFailureLogging_RemainsProductionError()
        {
            var terminalPort = GetUnusedPort();
            var requestId = "terminal-default-failure-" + Guid.NewGuid().ToString("N");
            var path = "/terminal-default-failure-" + Guid.NewGuid().ToString("N");
            Logger.SetMinLevel("debug");
            try
            {
                using (var client = new TerminalClient())
                {
                    var result = await client.PostJsonAsync(
                        "http://127.0.0.1:" + terminalPort, path,
                        "{\"request_id\":\"" + requestId + "\"}",
                        timeoutMs: 1000,
                        isRecoveryAttempt: false,
                        suppressProductionFailureLog: false).ConfigureAwait(false);

                    Assert.IsFalse(result.ok);
                    Logger.Flush(5000);
                    var lines = ReadLogLinesForRequest(requestId);
                    Assert.IsTrue(lines.Any(line => line.Contains("[终端通信][错误]")),
                        string.Join(Environment.NewLine, lines));
                }
            }
            finally
            {
                Logger.Flush(5000);
                Logger.SetMinLevel("info");
            }
        }

        [TestMethod]
        public async Task TerminalClient_RecoveryFailure_IsDebugOnly()
        {
            var terminalPort = GetUnusedPort();
            var requestId = "terminal-recovery-failure-" + Guid.NewGuid().ToString("N");
            var path = "/terminal-recovery-failure-" + Guid.NewGuid().ToString("N");
            Logger.SetMinLevel("debug");
            try
            {
                using (var client = new TerminalClient())
                {
                    var result = await client.PostJsonAsync(
                        "http://127.0.0.1:" + terminalPort, path,
                        "{\"request_id\":\"" + requestId + "\"}",
                        timeoutMs: 1000,
                        isRecoveryAttempt: true,
                        suppressProductionFailureLog: false).ConfigureAwait(false);

                    Assert.IsFalse(result.ok);
                    Logger.Flush(5000);
                    var lines = ReadLogLinesForRequest(requestId);
                    Assert.IsTrue(lines.Any(line => line.Contains("[终端通信][调试]")),
                        string.Join(Environment.NewLine, lines));
                    Assert.IsFalse(lines.Any(line =>
                        line.Contains("[终端通信][警告]") ||
                        line.Contains("[终端通信][错误]")),
                        string.Join(Environment.NewLine, lines));
                }
            }
            finally
            {
                Logger.Flush(5000);
                Logger.SetMinLevel("info");
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

        private static async Task AssertInitialStartOfflineUrlFailureLogAsync(
            PreviewResourceType resourceType)
        {
            var terminalPort = GetUnusedPort();
            var requestId = "initial-url-failure-" + resourceType + "-" +
                            Guid.NewGuid().ToString("N");
            try
            {
                using (var client = new TerminalClient())
                using (var host = new Form())
                using (var manager = new PreviewManager(client))
                {
                    Logger.SetMinLevel("debug");
                    var started = await manager.StartPreview(
                        resourceType, PreviewSessionType.External,
                        host.Handle, "http://127.0.0.1:" + terminalPort,
                        requestId: requestId).ConfigureAwait(false);

                    Assert.IsFalse(started);
                    Assert.AreEqual(1, manager.ActiveSessionCount);
                    Assert.IsTrue(await WaitUntilAsync(
                        () => manager.ActiveRecoveryCount > 0, 3000)
                        .ConfigureAwait(false));

                    Logger.Flush(5000);
                    var lines = ReadLogLinesForRequest(requestId);
                    var previewWarnings = lines
                        .Where(line => line.Contains("[预览][警告]"))
                        .ToArray();
                    var urlFailures = lines
                        .Where(line => line.Contains("预览地址请求失败"))
                        .ToArray();
                    var terminalWarnings = lines
                        .Where(line => line.Contains("[终端通信][警告]"))
                        .ToArray();
                    var terminalErrors = lines
                        .Where(line => line.Contains("[终端通信][错误]"))
                        .ToArray();
                    var terminalFailureLines = lines
                        .Where(line => line.Contains("POST网络错误") ||
                                       line.Contains("POST超时") ||
                                       line.Contains("POST失败：") ||
                                       line.Contains("POST异常："))
                        .ToArray();
                    var productionFailureLines = lines
                        .Where(line => line.Contains("[预览][信息]") ||
                                       line.Contains("[预览][警告]") ||
                                       line.Contains("[预览][错误]") ||
                                       line.Contains("[终端通信][信息]") ||
                                       line.Contains("[终端通信][警告]") ||
                                       line.Contains("[终端通信][错误]"))
                        .ToArray();

                    Assert.AreEqual(1, previewWarnings.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.AreEqual(1, productionFailureLines.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.AreEqual(0, terminalWarnings.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.AreEqual(0, terminalErrors.Length,
                        string.Join(Environment.NewLine, lines));
                    Assert.IsTrue(terminalFailureLines.Length >= 1,
                        string.Join(Environment.NewLine, lines));
                    Assert.IsTrue(terminalFailureLines.All(line => line.Contains("[调试]")),
                        string.Join(Environment.NewLine, lines));
                    Assert.IsTrue(urlFailures.Length >= 1,
                        string.Join(Environment.NewLine, lines));
                    Assert.IsTrue(urlFailures.All(line => line.Contains("[调试]")),
                        string.Join(Environment.NewLine, lines));
                    Assert.IsTrue(previewWarnings[0].Contains("正在自动恢复"));
                    Assert.IsFalse(lines.Any(line =>
                        line.Contains("[预览][警告]") &&
                        line.Contains("预览地址请求失败")));

                    Assert.IsTrue(await manager.StopPreviewAsync(
                        resourceType, PreviewSessionType.External)
                        .ConfigureAwait(false));
                }
            }
            finally
            {
                Logger.Flush(5000);
                Logger.SetMinLevel("info");
            }
        }

        private static string[] ReadLogLinesForRequest(string requestId)
        {
            return Directory.GetFiles(Logger.LogDirectory,
                    "HZCYKJTHardWareExe_*.log")
                .SelectMany(ReadSharedLines)
                .Where(line => line.IndexOf(requestId,
                    StringComparison.Ordinal) >= 0)
                .ToArray();
        }

        private static IEnumerable<string> ReadSharedLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    yield return line;
            }
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
