using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Server;
using HZCYKJTHardWare.Proxy.Terminal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class TerminalCallbackRoutingTests
    {
        [TestMethod]
        public async Task ProcessCallback_AfterSwitchAtoBtoA_IsDeliveredAgain()
        {
            var terminalManager = new TerminalManager();
            using (var terminalClient = new TerminalClient())
            using (var requestRegistry = new RequestRegistry())
            using (var processRegistry = new TerminalProcessRegistry())
            {
                var captureHandler = new CapturingHandler();
                using (var httpClient = new HttpClient(captureHandler))
                using (var callbackSender = new DllCallbackSender(httpClient,
                    "http://127.0.0.1:39091"))
                {
                    var callbackHandler = new TerminalCallbackHandler(
                        terminalClient, terminalManager, callbackSender,
                        requestRegistry, processRegistry, _ => { });
                    var routeA = terminalManager.CurrentRoute;
                    var sourceA = IPAddress.Parse(new System.Uri(routeA.BaseUrl).Host);
                    var terminalA = routeA.TerminalIndex;
                    var terminalB = terminalA == 1 ? 2 : 1;

                    var processA = processRegistry.Prepare(terminalA,
                        routeA.BaseUrl, "PROCESS_A",
                        @"C:\capture-a", 0);
                    Assert.IsTrue(processRegistry.Commit(processA));

                    await callbackHandler.HandleAsync(
                        NfcBody("PROCESS_A", "CARD-A-1"),
                        sourceA);
                    Assert.AreEqual(1, captureHandler.Count);

                    Assert.IsTrue(terminalManager.SwitchTo(terminalB));
                    var routeB = terminalManager.CurrentRoute;
                    var processB = processRegistry.Prepare(terminalB,
                        routeB.BaseUrl, "PROCESS_B",
                        @"C:\capture-b", 1);
                    Assert.IsTrue(processRegistry.Commit(processB));

                    await callbackHandler.HandleAsync(
                        NfcBody("PROCESS_A", "CARD-A-INACTIVE"),
                        sourceA);
                    Assert.AreEqual(1, captureHandler.Count,
                        "inactive terminal callback must not cross routes");

                    Assert.IsTrue(terminalManager.SwitchTo(terminalA));
                    await callbackHandler.HandleAsync(
                        NfcBody("PROCESS_A", "CARD-A-2"),
                        sourceA);

                    Assert.AreEqual(2, captureHandler.Count);
                    var requestIds = captureHandler.RequestIds;
                    Assert.AreEqual(2, requestIds.Count);
                    Assert.AreNotEqual(requestIds[0], requestIds[1],
                        "each process event needs a unique DLL delivery id");
                }
            }
        }

        [TestMethod]
        public async Task DisabledIcCardCallback_DoesNotSendAndDoesNotReplay()
        {
            var terminalManager = new TerminalManager();
            using (var terminalClient = new TerminalClient())
            using (var requestRegistry = new RequestRegistry())
            using (var processRegistry = new TerminalProcessRegistry())
            {
                var captureHandler = new CapturingHandler();
                using (var httpClient = new HttpClient(captureHandler))
                using (var callbackSender = new DllCallbackSender(httpClient,
                    "http://127.0.0.1:39091"))
                {
                    var callbackEnabled = false;
                    var callbackHandler = new TerminalCallbackHandler(
                        terminalClient, terminalManager, callbackSender,
                        requestRegistry, processRegistry, _ => { },
                        () => callbackEnabled);

                    var request = requestRegistry.Register(
                        "NFC_DISABLED", ProxyResourceTypes.NfcCard,
                        @"C:\capture", "http://127.0.0.1:39091/nfc-card", 0);
                    Assert.IsNotNull(request);

                    await callbackHandler.HandleAsync(
                        NfcBody("NFC_DISABLED", "CARD-DISABLED"), null);

                    Assert.AreEqual(0, captureHandler.Count,
                        "关闭开关时不得向 DLL 回调出口发送 IC 卡数据");
                    Assert.AreEqual(0, requestRegistry.ActiveCount,
                        "被拦截的一次性请求必须完成收尾");

                    callbackEnabled = true;
                    await callbackHandler.HandleAsync(
                        NfcBody("NFC_DISABLED", "CARD-REPLAY"), null);
                    Assert.AreEqual(0, captureHandler.Count,
                        "重新启用后不得补发关闭期间已消费的历史请求");

                    var newRequest = requestRegistry.Register(
                        "NFC_ENABLED", ProxyResourceTypes.NfcCard,
                        @"C:\capture", "http://127.0.0.1:39091/nfc-card", 0);
                    Assert.IsNotNull(newRequest);
                    await callbackHandler.HandleAsync(
                        NfcBody("NFC_ENABLED", "CARD-NEW"), null);
                    Assert.AreEqual(1, captureHandler.Count,
                        "重新启用后新收到的 IC 卡数据应正常回调");
                }
            }
        }

        private static string NfcBody(string requestId, string cardText)
        {
            return "{\"request_id\":\"" + requestId +
                "\",\"resource_type\":\"nfc_card\",\"card_text\":\"" +
                cardText + "\"}";
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly object _sync = new object();
            private readonly List<string> _requestIds = new List<string>();

            internal int Count
            {
                get { lock (_sync) return _requestIds.Count; }
            }

            internal List<string> RequestIds
            {
                get { lock (_sync) return new List<string>(_requestIds); }
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = await request.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);
                var marker = "\"request_id\":\"";
                var start = body.IndexOf(marker,
                    System.StringComparison.Ordinal) + marker.Length;
                var end = body.IndexOf('"', start);
                var requestId = body.Substring(start, end - start);
                lock (_sync) _requestIds.Add(requestId);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
        }
    }
}
