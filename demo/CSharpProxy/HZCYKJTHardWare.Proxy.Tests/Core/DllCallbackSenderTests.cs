using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

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

        [TestMethod]
        public async Task IdCardOcr_AddsPersonAndOpticalFields()
        {
            var handler = new CapturingHandler();
            var ocr = new OcrCallbackResult
            {
                CardType = 30,
                Name = "CHAN TAI MAN",
                Sex = "M",
                CardId = "A123456(7)",
                Birthday = "20000101",
                DateOfIssue = "20260101",
                AuthenScore = 1000,
                OpticalCheckResult = 0
            };
            using (var client = new HttpClient(handler))
            using (var sender = new DllCallbackSender(client, "http://127.0.0.1:39091"))
            using (var timeout = new CancellationTokenSource(5000))
            {
                var result = await sender.SendOcrResult("ocr-id-001", "", @"C:\ocr", ocr,
                    timeout.Token);

                Assert.AreEqual(CallbackDeliveryResult.Delivered, result);
                var body = JObject.Parse(handler.Body);
                Assert.AreEqual("$A123456(7)^1000^20000101^20260101^CHAN TAI MAN^M",
                    body["mrz"].Value<string>());
                Assert.AreEqual(30, body["card_type"].Value<int>());
                Assert.AreEqual("CHAN TAI MAN", body["name"].Value<string>());
                Assert.AreEqual("A123456(7)", body["cardId"].Value<string>());
                Assert.AreEqual("20260101", body["dateOfissue"].Value<string>());
                Assert.AreEqual(1000, body["authen_score"].Value<int>());
                Assert.AreEqual(0, body["optical_check_result"].Value<int>());
            }
        }

        [TestMethod]
        public async Task IdCardOcr_MissingFields_PreservesCompatibilitySlots()
        {
            var handler = new CapturingHandler();
            var ocr = new OcrCallbackResult { CardType = 30 };
            using (var client = new HttpClient(handler))
            using (var sender = new DllCallbackSender(client, "http://127.0.0.1:39091"))
            using (var timeout = new CancellationTokenSource(5000))
            {
                var result = await sender.SendOcrResult("ocr-id-empty", "ignored", @"C:\ocr",
                    ocr, timeout.Token);

                Assert.AreEqual(CallbackDeliveryResult.Delivered, result);
                var body = JObject.Parse(handler.Body);
                Assert.AreEqual("$^-1^^^^", body["mrz"].Value<string>());
                Assert.AreEqual(-1, body["authen_score"].Value<int>());
            }
        }

        [TestMethod]
        public async Task LegacyOcrCallback_DoesNotAddIdCardFields()
        {
            var handler = new CapturingHandler();
            using (var client = new HttpClient(handler))
            using (var sender = new DllCallbackSender(client, "http://127.0.0.1:39091"))
            using (var timeout = new CancellationTokenSource(5000))
            {
                var result = await sender.SendOcrResult("ocr-old-001", "A^B^C", @"C:\ocr",
                    timeout.Token);

                Assert.AreEqual(CallbackDeliveryResult.Delivered, result);
                var body = JObject.Parse(handler.Body);
                Assert.AreEqual("A^B^C", body["mrz"].Value<string>());
                Assert.IsNull(body["card_type"]);
                Assert.IsNull(body["authen_score"]);
                Assert.IsNull(body["optical_check_result"]);
                Assert.IsNull(body["name"]);
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

        private sealed class CapturingHandler : HttpMessageHandler
        {
            internal string Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
        }
    }
}
