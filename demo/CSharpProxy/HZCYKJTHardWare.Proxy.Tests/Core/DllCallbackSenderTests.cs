using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class DllCallbackSenderTests
    {
        [TestMethod]
        public async Task ServiceUnavailable_IsNotRetried()
        {
            var handler = new SequenceHandler(call => new HttpResponseMessage(
                call == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted));
            using (var client = new HttpClient(handler))
            using (var sender = new DllCallbackSender(client, "http://127.0.0.1:39091"))
            using (var timeout = new CancellationTokenSource(5000))
            {
                var result = await sender.SendNfcResult("retry-001", "card", timeout.Token);

                Assert.AreEqual(CallbackDeliveryResult.Failed, result);
                Assert.AreEqual(1, handler.CallCount);
            }
        }

        [TestMethod]
        public async Task ClientFailure_IsNotRetried()
        {
            var handler = new SequenceHandler(call =>
                new HttpResponseMessage(HttpStatusCode.BadRequest));
            using (var client = new HttpClient(handler))
            using (var sender = new DllCallbackSender(client, "http://127.0.0.1:39091"))
            using (var timeout = new CancellationTokenSource(5000))
            {
                var result = await sender.SendNfcResult("bad-001", "card", timeout.Token);

                Assert.AreEqual(CallbackDeliveryResult.Failed, result);
                Assert.AreEqual(1, handler.CallCount);
            }
        }

        [TestMethod]
        public async Task InvalidBaseUrl_IsNotSent()
        {
            var handler = new SequenceHandler(call =>
                new HttpResponseMessage(HttpStatusCode.Accepted));
            using (var client = new HttpClient(handler))
            using (var sender = new DllCallbackSender(client, "not-an-absolute-url"))
            using (var timeout = new CancellationTokenSource(5000))
            {
                var result = await sender.SendNfcResult("bad-url-001", "card", timeout.Token);

                Assert.AreEqual(CallbackDeliveryResult.Failed, result);
                Assert.AreEqual(0, handler.CallCount);
            }
        }

        private sealed class SequenceHandler : HttpMessageHandler
        {
            private readonly System.Func<int, HttpResponseMessage> _responseFactory;
            private int _callCount;

            internal SequenceHandler(System.Func<int, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            internal int CallCount => Volatile.Read(ref _callCount);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var call = Interlocked.Increment(ref _callCount);
                return Task.FromResult(_responseFactory(call));
            }
        }
    }
}
