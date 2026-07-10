using HZCYKJTHardWare.Proxy.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class OcrIdCardCallbackTests
    {
        [TestMethod]
        public void CardType30_CompleteOptics_PassedAndPersonFieldsParsed()
        {
            var result = CallbackParser.ParseOcrDocument(IdCardJson(
                "{\"authen_score\":1000,\"optical_check_result\":0}"));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(30, result.CardType);
            Assert.AreEqual("CHAN TAI MAN", result.Name);
            Assert.AreEqual("M", result.Sex);
            Assert.AreEqual("A123456(7)", result.CardId);
            Assert.AreEqual("20000101", result.Birthday);
            Assert.AreEqual("20260101", result.DateOfIssue);
            Assert.AreEqual(1000, result.AuthenScore);
            Assert.AreEqual(0, result.OpticalCheckResult);
        }

        [TestMethod]
        public void CardType30_OpticalCheckFailed_IsOne()
        {
            var result = CallbackParser.ParseOcrDocument(IdCardJson(
                "{\"authen_score\":800,\"optical_check_result\":1}"));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(800, result.AuthenScore);
            Assert.AreEqual(1, result.OpticalCheckResult);
        }

        [TestMethod]
        public void CardType30_MissingOptics_UsesUnknownDefaults()
        {
            var result = CallbackParser.ParseOcrDocument(IdCardJson(null));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(-1, result.AuthenScore);
            Assert.AreEqual(-1, result.OpticalCheckResult);
        }

        [TestMethod]
        public void CardType30_NullOpticsFields_UsesUnknownDefaults()
        {
            var result = CallbackParser.ParseOcrDocument(IdCardJson(
                "{\"authen_score\":null,\"optical_check_result\":null}"));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(-1, result.AuthenScore);
            Assert.AreEqual(-1, result.OpticalCheckResult);
        }

        [TestMethod]
        public void NonIdCard_WithoutOptics_PreservesExistingOcrResult()
        {
            const string json = "{\"request_id\":\"passport-001\",\"resource_type\":\"ocr_document\"," +
                "\"data\":{\"card_type\":1,\"MRZ1\":\"P<CHNTEST\",\"MRZ2\":\"ABC123\"," +
                "\"evidence_images\":[]}}";

            var result = CallbackParser.ParseOcrDocument(json);

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(1, result.CardType);
            Assert.AreEqual("P<CHNTEST^ABC123^", result.Mrz);
            Assert.AreEqual(-1, result.AuthenScore);
            Assert.AreEqual(-1, result.OpticalCheckResult);
            Assert.AreEqual("", result.Name);
        }

        [TestMethod]
        public void ExistingOcrWithoutCardType_RemainsValid()
        {
            const string json = "{\"request_id\":\"legacy-001\",\"resource_type\":\"ocr_document\"," +
                "\"mrz\":\"OLD1^OLD2^OLD3\"}";

            var result = CallbackParser.ParseOcrDocument(json);

            Assert.IsTrue(result.Valid);
            Assert.AreEqual("OLD1^OLD2^OLD3", result.Mrz);
            Assert.AreEqual(-1, result.CardType);
            Assert.AreEqual(-1, result.AuthenScore);
            Assert.AreEqual(-1, result.OpticalCheckResult);
        }

        [TestMethod]
        public void CardType30_InvalidFieldTypes_DoNotBecomePassedOrThrow()
        {
            var result = CallbackParser.ParseOcrDocument(IdCardJson(
                "{\"authen_score\":\"1000\",\"optical_check_result\":false}"));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(30, result.CardType);
            Assert.AreEqual(-1, result.AuthenScore);
            Assert.AreEqual(-1, result.OpticalCheckResult);

            var unsupportedResult = CallbackParser.ParseOcrDocument(IdCardJson(
                "{\"authen_score\":1000,\"optical_check_result\":2}"));
            Assert.AreEqual(-1, unsupportedResult.OpticalCheckResult);

            var invalidCardType = CallbackParser.ParseOcrDocument(
                "{\"request_id\":\"bad-card-type\",\"resource_type\":\"ocr_document\"," +
                "\"data\":{\"card_type\":\"30\",\"optics_authen\":" +
                "{\"authen_score\":1000,\"optical_check_result\":0}}}");
            Assert.IsTrue(invalidCardType.Valid);
            Assert.AreEqual(-1, invalidCardType.CardType);
            Assert.AreEqual(-1, invalidCardType.OpticalCheckResult);
        }

        private static string IdCardJson(string opticsAuthen)
        {
            var optics = opticsAuthen == null ? "" : ",\"optics_authen\":" + opticsAuthen;
            return "{\"request_id\":\"ocr-doc-hk-001\",\"resource_type\":\"ocr_document\"," +
                "\"data\":{\"card_type\":30,\"person_info\":[{" +
                "\"name\":\"CHAN TAI MAN\",\"sex\":\"M\",\"cardId\":\"A123456(7)\"," +
                "\"birthday\":\"20000101\",\"dateOfissue\":\"20260101\"}]," +
                "\"evidence_images\":[]" + optics + "}}";
        }
    }
}
