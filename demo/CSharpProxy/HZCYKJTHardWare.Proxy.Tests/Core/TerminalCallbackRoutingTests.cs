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

                    var processA = processRegistry.Prepare(1,
                        "http://192.168.20.30:9098", "PROCESS_A",
                        @"C:\capture-a", 0);
                    Assert.IsTrue(processRegistry.Commit(processA));

                    await callbackHandler.HandleAsync(
                        NfcBody("PROCESS_A", "CARD-A-1"));
                    Assert.AreEqual(1, captureHandler.Count);

                    Assert.IsTrue(terminalManager.SwitchTo(2));
                    var processB = processRegistry.Prepare(2,
                        "http://192.168.20.31:9098", "PROCESS_B",
                        @"C:\capture-b", 1);
                    Assert.IsTrue(processRegistry.Commit(processB));

                    await callbackHandler.HandleAsync(
                        NfcBody("PROCESS_A", "CARD-A-INACTIVE"));
                    Assert.AreEqual(1, captureHandler.Count,
                        "inactive terminal callback must not cross routes");

                    Assert.IsTrue(terminalManager.SwitchTo(1));
                    await callbackHandler.HandleAsync(
                        NfcBody("PROCESS_A", "CARD-A-2"));

                    Assert.AreEqual(2, captureHandler.Count);
                    var requestIds = captureHandler.RequestIds;
                    Assert.AreEqual(2, requestIds.Count);
                    Assert.AreNotEqual(requestIds[0], requestIds[1],
                        "each process event needs a unique DLL delivery id");
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
