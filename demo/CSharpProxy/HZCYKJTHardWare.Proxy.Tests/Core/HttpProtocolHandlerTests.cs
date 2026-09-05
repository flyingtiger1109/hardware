using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class HttpProtocolHandlerTests
    {
        [TestMethod]
        public async Task ReadHttpRequestAsync_ReadsFragmentedUtf8BodyExactly()
        {
            var body = "{\"request_id\":\"分片-测试\",\"message\":\"中文与特殊字符：门禁🔒\"}";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header = "POST /capture/face HTTP/1.1\r\n" +
                "Host: 127.0.0.1\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n\r\n";

            var request = await RunReaderAsync(header, bodyBytes, async stream =>
            {
                var chunkSizes = new[] { 1, 2, 3, 5, 8, 13 };
                var offset = 0;
                var chunkIndex = 0;
                while (offset < bodyBytes.Length)
                {
                    var count = Math.Min(chunkSizes[chunkIndex % chunkSizes.Length],
                        bodyBytes.Length - offset);
                    await stream.WriteAsync(bodyBytes, offset, count)
                        .ConfigureAwait(false);
                    offset += count;
                    chunkIndex++;
                    await Task.Delay(1).ConfigureAwait(false);
                }
            });

            Assert.AreEqual("POST", request.method);
            Assert.AreEqual("/capture/face", request.path);
            Assert.AreEqual(body, request.body);
        }

        [TestMethod]
        public async Task ReadHttpRequestAsync_ReassemblesOneByteBodyFragments()
        {
            var body = new string('x', 500);
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header = "POST /process/start HTTP/1.1\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n\r\n";

            var request = await RunReaderAsync(header, bodyBytes, async stream =>
            {
                for (var i = 0; i < bodyBytes.Length; i++)
                {
                    await stream.WriteAsync(bodyBytes, i, 1).ConfigureAwait(false);
                }
            });

            Assert.AreEqual(500, request.body.Length);
            Assert.AreEqual(body, request.body);
        }

        [TestMethod]
        public async Task ReadHttpRequestAsync_IncompleteBodyReturnsReadFailure()
        {
            var bodyBytes = Encoding.UTF8.GetBytes(new string('b', 200));
            var header = "POST /preview/start HTTP/1.1\r\n" +
                "Content-Length: 500\r\n\r\n";
            var failure = await RunIncompleteBodyAsync(header, bodyBytes);

            Assert.IsNotNull(failure);
            Assert.AreEqual("body_incomplete", failure.FailureCode);
            Assert.AreEqual("/preview/start", failure.Path);
            Assert.AreEqual(200L, failure.ReceivedBytes);
            Assert.AreEqual(500L, failure.ExpectedBytes);
        }

        [DataTestMethod]
        [DataRow("", "content_length_invalid")]
        [DataRow("-1", "content_length_invalid")]
        [DataRow("not-a-number", "content_length_invalid")]
        [DataRow("999999999999999999999", "content_length_too_large")]
        public async Task ReadHttpRequestAsync_RejectsInvalidContentLength(
            string contentLength, string expectedFailureCode)
        {
            var request = "POST /authorize HTTP/1.1\r\n" +
                "Content-Length: " + contentLength + "\r\n\r\n";
            var failure = await RunFailureAsync(request);

            Assert.IsNotNull(failure);
            Assert.AreEqual(expectedFailureCode, failure.FailureCode);
        }

        [TestMethod]
        public async Task ReadHttpRequestAsync_MissingLengthWithoutBodyPreservesEmptyBody()
        {
            var request = await RunReaderAsync(
                "GET /ping HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n",
                new byte[0],
                stream => Task.FromResult(0));

            Assert.AreEqual("GET", request.method);
            Assert.AreEqual("/ping", request.path);
            Assert.AreEqual("", request.body);
        }

        [TestMethod]
        public async Task ReadHttpRequestAsync_HeaderEofReturnsReadFailure()
        {
            var failure = await RunIncompleteBodyAsync(
                "POST /capture/fingerprint HTTP/1.1\r\nContent-Length: 1\r\n",
                new byte[0]);

            Assert.IsNotNull(failure);
            Assert.AreEqual("header_incomplete", failure.FailureCode);
        }

        [TestMethod]
        public async Task ProxyServer_IncompleteDllBodyReturns400BeforeBusinessDispatch()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient();
            TcpClient serverClient = null;
            ProxyServer proxy = null;
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var acceptTask = listener.AcceptTcpClientAsync();
                var connectTask = client.ConnectAsync(endpoint.Address, endpoint.Port);
                await Task.WhenAll(acceptTask, connectTask).ConfigureAwait(false);
                serverClient = acceptTask.Result;

                proxy = new ProxyServer(_ => { });
                var handler = typeof(ProxyServer).GetMethod("HandleDllRequest",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(handler);
                var handlerTask = (Task)handler.Invoke(proxy,
                    new object[] { serverClient });

                var header = Encoding.ASCII.GetBytes(
                    "POST /process/start HTTP/1.1\r\n" +
                    "Content-Length: 500\r\n\r\n");
                var partialBody = Encoding.UTF8.GetBytes(new string('p', 200));
                var clientStream = client.GetStream();
                await clientStream.WriteAsync(header, 0, header.Length)
                    .ConfigureAwait(false);
                await clientStream.WriteAsync(partialBody, 0, partialBody.Length)
                    .ConfigureAwait(false);
                client.Client.Shutdown(SocketShutdown.Send);

                var response = await ReadResponseAsync(clientStream).ConfigureAwait(false);
                await handlerTask.ConfigureAwait(false);

                StringAssert.StartsWith(response, "HTTP/1.1 400 ");
                StringAssert.Contains(response, "\"invalid_request\"");
            }
            finally
            {
                proxy?.Dispose();
                serverClient?.Dispose();
                client.Dispose();
                listener.Stop();
            }
        }

        private static async Task<(string method, string path, string body)> RunReaderAsync(
            string header, byte[] body, Func<NetworkStream, Task> writeBody)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient();
            TcpClient serverClient = null;
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var acceptTask = listener.AcceptTcpClientAsync();
                var connectTask = client.ConnectAsync(endpoint.Address, endpoint.Port);
                await Task.WhenAll(acceptTask, connectTask).ConfigureAwait(false);
                serverClient = acceptTask.Result;

                var parseTask = HttpProtocolHandler.ReadHttpRequestAsync(
                    serverClient.GetStream());
                var clientStream = client.GetStream();
                var headerBytes = Encoding.ASCII.GetBytes(header);
                await clientStream.WriteAsync(headerBytes, 0, headerBytes.Length)
                    .ConfigureAwait(false);
                await writeBody(clientStream).ConfigureAwait(false);
                return await parseTask.ConfigureAwait(false);
            }
            finally
            {
                serverClient?.Dispose();
                client.Dispose();
                listener.Stop();
            }
        }

        private static async Task<HttpRequestReadException> RunIncompleteBodyAsync(
            string header, byte[] body)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient();
            TcpClient serverClient = null;
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var acceptTask = listener.AcceptTcpClientAsync();
                var connectTask = client.ConnectAsync(endpoint.Address, endpoint.Port);
                await Task.WhenAll(acceptTask, connectTask).ConfigureAwait(false);
                serverClient = acceptTask.Result;

                var parseTask = HttpProtocolHandler.ReadHttpRequestAsync(
                    serverClient.GetStream());
                var clientStream = client.GetStream();
                var headerBytes = Encoding.ASCII.GetBytes(header);
                await clientStream.WriteAsync(headerBytes, 0, headerBytes.Length)
                    .ConfigureAwait(false);
                if (body.Length > 0)
                {
                    await clientStream.WriteAsync(body, 0, body.Length)
                        .ConfigureAwait(false);
                }
                client.Client.Shutdown(SocketShutdown.Send);

                try
                {
                    await parseTask.ConfigureAwait(false);
                    return null;
                }
                catch (HttpRequestReadException ex)
                {
                    return ex;
                }
            }
            finally
            {
                serverClient?.Dispose();
                client.Dispose();
                listener.Stop();
            }
        }

        private static async Task<HttpRequestReadException> RunFailureAsync(
            string request)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient();
            TcpClient serverClient = null;
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var acceptTask = listener.AcceptTcpClientAsync();
                var connectTask = client.ConnectAsync(endpoint.Address, endpoint.Port);
                await Task.WhenAll(acceptTask, connectTask).ConfigureAwait(false);
                serverClient = acceptTask.Result;

                var parseTask = HttpProtocolHandler.ReadHttpRequestAsync(
                    serverClient.GetStream());
                var requestBytes = Encoding.ASCII.GetBytes(request);
                await client.GetStream().WriteAsync(requestBytes, 0, requestBytes.Length)
                    .ConfigureAwait(false);

                try
                {
                    await parseTask.ConfigureAwait(false);
                    return null;
                }
                catch (HttpRequestReadException ex)
                {
                    return ex;
                }
            }
            finally
            {
                serverClient?.Dispose();
                client.Dispose();
                listener.Stop();
            }
        }

        private static async Task<string> ReadResponseAsync(NetworkStream stream)
        {
            using (var response = new MemoryStream())
            {
                var buffer = new byte[1024];
                int read;
                do
                {
                    read = await stream.ReadAsync(buffer, 0, buffer.Length)
                        .ConfigureAwait(false);
                    if (read > 0)
                        response.Write(buffer, 0, read);
                }
                while (read > 0);
                return Encoding.UTF8.GetString(response.ToArray());
            }
        }
    }
}
