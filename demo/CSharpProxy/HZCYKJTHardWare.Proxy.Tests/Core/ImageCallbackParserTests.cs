using HZCYKJTHardWare.Proxy.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Core
{
    [TestClass]
    public class ImageCallbackParserTests
    {
        [TestMethod]
        public void ParseFingerprint_ExtractsAllFieldsFromSingleResponseModel()
        {
            const string json = "{" +
                "\"request_id\":\"FP_001\"," +
                "\"save_path\":\"fingerprint.jpg\"," +
                "\"undistorted_image_base64\":\"top-level-raw\"," +
                "\"data\":{" +
                    "\"image_base64\":\"main-image\"," +
                    "\"image_mime_type\":\"image/jpeg\"," +
                    "\"undistorted_image_base64\":\"nested-raw\"}}";

            var result = CallbackParser.ParseImageCapture(json,
                "fingerprint_image");

            Assert.IsTrue(result.Valid);
            Assert.AreEqual("FP_001", result.RequestId);
            Assert.AreEqual("fingerprint.jpg", result.SavePath);
            Assert.AreEqual("main-image", result.ImageBase64);
            Assert.AreEqual("image/jpeg", result.ImageMimeType);
            Assert.AreEqual("top-level-raw", result.UndistortedImageBase64);
        }

        [TestMethod]
        public void ParseFingerprint_FallsBackToNestedUndistortedImage()
        {
            const string json = "{" +
                "\"request_id\":\"FP_002\"," +
                "\"data\":{" +
                    "\"fingerprint_capture\":\"main-image\"," +
                    "\"undistorted_image_base64\":\"nested-raw\"}}";

            var result = CallbackParser.ParseImageCapture(json,
                "fingerprint_image");

            Assert.IsTrue(result.Valid);
            Assert.AreEqual("main-image", result.ImageBase64);
            Assert.AreEqual("nested-raw", result.UndistortedImageBase64);
            Assert.AreEqual("image/jpeg", result.ImageMimeType);
        }

        [TestMethod]
        public void ParseFace_PreservesTopLevelSavePathFallback()
        {
            const string json = "{" +
                "\"save_path\":\"face.bmp\"," +
                "\"data\":{\"face_capture\":\"face-image\"}}";

            var result = CallbackParser.ParseImageCapture(json, "face_image");

            Assert.IsTrue(result.Valid);
            Assert.AreEqual("face.bmp", result.SavePath);
            Assert.AreEqual("face-image", result.ImageBase64);
        }
    }
}
