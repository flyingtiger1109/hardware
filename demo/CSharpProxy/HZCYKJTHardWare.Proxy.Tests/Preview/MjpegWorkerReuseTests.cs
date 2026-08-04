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
                Assert.AreEqual(createdBefore + 1, MjpegPreviewController.CreatedWorkerCount);
                Assert.AreEqual(renderBefore + 1, MjpegPreviewController.LiveRenderThreadCount);
                Assert.AreEqual(readerBefore + 1, MjpegPreviewController.LiveReaderThreadCount);

                Assert.IsTrue(await controller.SwitchStreamAsync(secondServer.Url, hostHwnd, 3000)
                    .ConfigureAwait(false));
                Assert.IsTrue(controller.IsRunning);
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
