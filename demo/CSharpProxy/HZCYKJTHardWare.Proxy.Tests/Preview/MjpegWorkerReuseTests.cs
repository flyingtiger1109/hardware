using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Terminal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class MjpegWorkerReuseTests
    {
        [TestMethod]
        public async Task SwitchStream_ReusesRenderAndReaderThreads()
        {
            using (var firstServer = new MjpegTestServer(Color.Red))
            using (var secondServer = new MjpegTestServer(Color.Blue))
            using (var host = new Form())
            {
                var hostHwnd = host.Handle;
                var createdBefore = MjpegPreviewController.CreatedWorkerCount;
                var renderBefore = MjpegPreviewController.LiveRenderThreadCount;
                var readerBefore = MjpegPreviewController.LiveReaderThreadCount;

                var controller = await MjpegPreviewController.StartAsync(
                    "reuse-test", firstServer.Url, hostHwnd,
                    2, 2, false, true, 3000).ConfigureAwait(false);

                Assert.IsNotNull(controller);
                Assert.IsTrue(controller.IsRunning);
                var firstRender = await controller.WaitForRenderedFrameAsync(
                    CancellationToken.None).ConfigureAwait(false);
                Assert.IsTrue(firstRender.Succeeded);
                Assert.AreEqual(createdBefore + 1, MjpegPreviewController.CreatedWorkerCount);
                Assert.AreEqual(renderBefore + 1, MjpegPreviewController.LiveRenderThreadCount);
                Assert.AreEqual(readerBefore + 1, MjpegPreviewController.LiveReaderThreadCount);

                Assert.IsTrue(await controller.SwitchStreamAsync(secondServer.Url, hostHwnd, 3000)
                    .ConfigureAwait(false));
                Assert.IsTrue(controller.IsRunning);
                var secondRender = await controller.WaitForRenderedFrameAsync(
                    CancellationToken.None).ConfigureAwait(false);
                Assert.IsTrue(secondRender.Succeeded);
                Assert.AreEqual(createdBefore + 1, MjpegPreviewController.CreatedWorkerCount);
                Assert.AreEqual(renderBefore + 1, MjpegPreviewController.LiveRenderThreadCount);
                Assert.AreEqual(readerBefore + 1, MjpegPreviewController.LiveReaderThreadCount);

                Assert.IsTrue(await controller.PauseAsync(1500).ConfigureAwait(false));
                Assert.IsFalse(controller.IsRunning);
                Assert.AreEqual(renderBefore + 1, MjpegPreviewController.LiveRenderThreadCount);
                Assert.AreEqual(readerBefore + 1, MjpegPreviewController.LiveReaderThreadCount);

                Assert.IsTrue(await controller.SwitchStreamAsync(firstServer.Url, hostHwnd, 3000)
                    .ConfigureAwait(false));
                Assert.AreEqual(createdBefore + 1, MjpegPreviewController.CreatedWorkerCount);

                await controller.DisposeAsync(3000).ConfigureAwait(false);
                Assert.AreEqual(renderBefore, MjpegPreviewController.LiveRenderThreadCount);
                Assert.AreEqual(readerBefore, MjpegPreviewController.LiveReaderThreadCount);
            }
        }

        [TestMethod]
        public async Task DestroyedRenderTarget_ReportsTargetFailureWithoutStreamRecovery()
        {
            using (var server = new MjpegTestServer(Color.Green))
            {
                var host = new Form();
                MjpegPreviewController controller = null;
                try
                {
                    host.CreateControl();
                    host.Show();
                    Application.DoEvents();
                    var hostHwnd = host.Handle;
                    controller = await MjpegPreviewController.StartAsync(
                        "destroyed-target-test", server.Url, hostHwnd,
                        2, 2, false, true, 3000,
                        directRenderTarget: true).ConfigureAwait(false);

                    Assert.IsNotNull(controller);
                    var renderReady = await controller.WaitForRenderedFrameAsync(
                        CancellationToken.None).ConfigureAwait(false);
                    Assert.IsTrue(renderReady.Succeeded);

                    var failure = new TaskCompletionSource<MjpegFailureKind>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    controller.SetFailureHandler((player, kind, reason) =>
                    {
                        if (kind == MjpegFailureKind.RenderTargetFailure)
                            failure.TrySetResult(kind);
                    });

                    host.Close();
                    host.Dispose();
                    var completed = await Task.WhenAny(failure.Task,
                        Task.Delay(3000)).ConfigureAwait(false);
                    Assert.AreSame(failure.Task, completed);
                    Assert.AreEqual(MjpegFailureKind.RenderTargetFailure,
                        await failure.Task.ConfigureAwait(false));
                }
                finally
                {
                    if (controller != null)
                        await controller.DisposeAsync(3000).ConfigureAwait(false);
                    host.Dispose();
                }
            }
        }

        [TestMethod]
        public async Task PreviewManager_EndsExternalSessionWhenRenderTargetIsDestroyed()
        {
            using (var server = new MjpegTestServer(Color.Purple))
            using (var client = new TerminalClient())
            using (var host = new Form())
            {
                host.CreateControl();
                host.Show();
                Application.DoEvents();

                using (var manager = new PreviewManager(client))
                {
                    var createdBefore = MjpegPreviewController.CreatedWorkerCount;
                    var failure = new TaskCompletionSource<string>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    manager.SetExternalPreviewFailureHandler((resourceType, requestId, reason) =>
                        failure.TrySetResult(reason));

                    var started = await manager.StartPreview(
                        PreviewResourceType.Camera, PreviewSessionType.External,
                        host.Handle, "", explicitPreviewUrl: server.Url,
                        requestId: "render-target-request").ConfigureAwait(false);
                    Assert.IsTrue(started);

                    host.Close();
                    host.Dispose();
                    var completed = await Task.WhenAny(failure.Task,
                        Task.Delay(3000)).ConfigureAwait(false);
                    Assert.AreSame(failure.Task, completed);
                    StringAssert.Contains(await failure.Task.ConfigureAwait(false), "销毁");
                    Assert.AreEqual(0, manager.ActiveSessionCount);
                    Assert.AreEqual(createdBefore + 1, MjpegPreviewController.CreatedWorkerCount);
                    Assert.AreEqual(0, manager.MjpegWorkerCount);
                }
            }
        }

        [TestMethod]
        public async Task ZeroClientRect_WaitsForRenderRetryWithoutRecreatingStream()
        {
            var hwnd = CreateWindowEx(0, "STATIC", "mjpeg-zero-target",
                WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero,
                GetModuleHandle(null), IntPtr.Zero);
            Assert.AreNotEqual(IntPtr.Zero, hwnd);

            using (var server = new MjpegTestServer(Color.Orange))
            {
                MjpegPreviewController controller = null;
                try
                {
                    controller = await MjpegPreviewController.StartAsync(
                        "zero-client-rect-test", server.Url, hwnd,
                        2, 2, false, true, 3000,
                        directRenderTarget: true).ConfigureAwait(false);
                    Assert.IsNotNull(controller);

                    Assert.IsTrue(await controller.SwitchStreamAsync(
                        server.Url, hwnd, 3000).ConfigureAwait(false));
                    using (var waitTimeout = new CancellationTokenSource(500))
                    {
                        var notReady = await controller.WaitForRenderedFrameAsync(
                            waitTimeout.Token).ConfigureAwait(false);
                        Assert.IsFalse(notReady.Succeeded);
                    }

                    Assert.IsTrue(SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 200, 200,
                        SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE));
                    var ready = await controller.WaitForRenderedFrameAsync(
                        CancellationToken.None).ConfigureAwait(false);
                    Assert.IsTrue(ready.Succeeded);
                    Assert.IsTrue(controller.IsRunning);
                }
                finally
                {
                    if (controller != null)
                        await controller.DisposeAsync(3000).ConfigureAwait(false);
                }
            }

            DestroyWindow(hwnd);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className,
            string windowName, uint style, int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hwnd);

        private const uint WS_POPUP = 0x80000000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private sealed class MjpegTestServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly byte[] _jpeg;
            private readonly Task _acceptTask;

            internal MjpegTestServer(Color color)
            {
                using (var bitmap = new Bitmap(2, 2))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var memory = new MemoryStream())
                {
                    graphics.Clear(color);
                    bitmap.Save(memory, ImageFormat.Jpeg);
                    _jpeg = memory.ToArray();
                }

                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                var endpoint = (IPEndPoint)_listener.LocalEndpoint;
                Url = $"http://127.0.0.1:{endpoint.Port}/preview";
                _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }

            internal string Url { get; }

            private async Task AcceptLoopAsync(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (SocketException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;
                        throw;
                    }

                    _ = Task.Run(() => ServeClientAsync(client, cancellationToken));
                }
            }

            private async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var header = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: multipart/x-mixed-replace; boundary=frame\r\n" +
                        "Connection: close\r\n\r\n");
                    var frameHeader = Encoding.ASCII.GetBytes(
                        $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {_jpeg.Length}\r\n\r\n");
                    var frameEnd = Encoding.ASCII.GetBytes("\r\n");

                    try
                    {
                        await stream.WriteAsync(header, 0, header.Length, cancellationToken)
                            .ConfigureAwait(false);
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            await stream.WriteAsync(frameHeader, 0, frameHeader.Length, cancellationToken)
                                .ConfigureAwait(false);
                            await stream.WriteAsync(_jpeg, 0, _jpeg.Length, cancellationToken)
                                .ConfigureAwait(false);
                            await stream.WriteAsync(frameEnd, 0, frameEnd.Length, cancellationToken)
                                .ConfigureAwait(false);
                            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                            await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }

            public void Dispose()
            {
                _cts.Cancel();
                _listener.Stop();
                try { _acceptTask.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
                _cts.Dispose();
            }
        }
    }
}
