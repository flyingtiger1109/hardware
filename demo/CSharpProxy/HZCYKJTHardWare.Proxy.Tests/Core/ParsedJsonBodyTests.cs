using HZCYKJTHardWare.Proxy.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class ParsedJsonBodyTests
    {
        [TestMethod]
        public void Parse_ValidCommandBody_ReusesFieldsAndPreservesRawBody()
        {
            const string body = "{\"request_id\":\"REQ-1\"," +
                "\"save_dir\":\"captures\\\\fingerprint.jpg\"," +
                "\"callback_url\":\"http://127.0.0.1/callback\"," +
                "\"terminal_index\":2,\"ZJHM\":\"DOC-001\",\"XM\":\"Tester\"}";

            var parsed = ParsedJsonBody.Parse(body);

            Assert.IsTrue(parsed.IsValid);
            Assert.AreEqual(body, parsed.RawBody);
            Assert.AreEqual("REQ-1", parsed.GetString("request_id"));
            Assert.AreEqual("captures\\fingerprint.jpg", parsed.GetString("save_dir"));
            Assert.AreEqual("http://127.0.0.1/callback", parsed.GetString("callback_url"));
            Assert.AreEqual(2, parsed.GetInt("terminal_index"));
            Assert.AreEqual("DOC-001", parsed.GetString("ZJHM"));
            Assert.AreEqual("Tester", parsed.GetString("XM"));
        }

        [TestMethod]
        public void Parse_MalformedCommandBody_PreservesLegacyStringFallback()
        {
            const string body = "{\"request_id\":\"REQ-FALLBACK\",\"save_dir\":\"captures\"";

            var parsed = ParsedJsonBody.Parse(body);

            Assert.IsFalse(parsed.IsValid);
            Assert.AreEqual("REQ-FALLBACK", parsed.GetString("request_id"));
            Assert.AreEqual("captures", parsed.GetString("save_dir"));
            Assert.AreEqual(0, parsed.GetInt("terminal_index"));
        }

        [TestMethod]
        public void ParseOcrDocument_ParsedRootPreservesMetadataAndEvidenceCompatibility()
        {
            const string body = "{\"request_id\":\"OCR-1\",\"data\":{" +
                "\"MRZ1\":\"LINE1\",\"MRZ2\":\"LINE2\",\"MRZ3\":\"LINE3\"," +
                "\"evidence_images\":[{\"lampType\":1,\"imageData\":\"AQID\"}]}}";
            var parsed = ParsedJsonBody.Parse(body);

            var result = CallbackParser.ParseOcrDocument(parsed.Root, parsed.RawBody);

            Assert.IsTrue(result.Valid);
            Assert.AreEqual("OCR-1", result.RequestId);
            Assert.AreEqual("LINE1^LINE2^LINE3", result.Mrz);
            Assert.IsNotNull(result.EvidenceImages);
            Assert.AreEqual(1, result.EvidenceImages.Count);
            StringAssert.Contains(result.EvidenceImages[0], "\"imageData\":\"AQID\"");
        }

        [TestMethod]
        public void ParseOcrDocument_InvalidRoot_ReturnsInvalidResult()
        {
            var parsed = ParsedJsonBody.Parse("{invalid-json");

            var result = CallbackParser.ParseOcrDocument(parsed.Root, parsed.RawBody);

            Assert.IsFalse(result.Valid);
        }
    }
}
