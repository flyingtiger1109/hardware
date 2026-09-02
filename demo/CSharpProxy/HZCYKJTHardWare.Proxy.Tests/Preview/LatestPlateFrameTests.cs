using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class LatestPlateFrameTests
    {
        [TestMethod]
        public void VlcArguments_DisableSnapshotPreviewOsdAndPreferJpeg()
        {
            var args = VlcPreviewPlayer.BuildLibVlcArguments(null, disableOsd: true);

            CollectionAssert.Contains(args, "--no-osd");
            CollectionAssert.Contains(args, "--no-snapshot-preview");
            CollectionAssert.Contains(args, "--snapshot-format=jpg");
        }

        [TestMethod]
        public void SnapshotImageNormalizer_ConvertsPngRegardlessOfJpgFileName()
        {
            var png = CreateImageBytes(ImageFormat.Png);

            var ok = SnapshotImageNormalizer.TryNormalizeToJpeg(png,
                out var jpeg, out var detectedFormat, out var width,
                out var height, out var failureReason);

            Assert.IsTrue(ok, failureReason);
            Assert.AreEqual("png", detectedFormat);
            Assert.AreEqual(2, width);
            Assert.AreEqual(1, height);
            Assert.IsTrue(JpegFrameValidator.TryGetDimensions(jpeg,
                out var outputWidth, out var outputHeight));
            Assert.AreEqual(2, outputWidth);
            Assert.AreEqual(1, outputHeight);
        }

        [TestMethod]
        public void SnapshotImageNormalizer_AcceptsRealJpeg()
        {
            var input = CreateImageBytes(ImageFormat.Jpeg);

            var ok = SnapshotImageNormalizer.TryNormalizeToJpeg(input,
                out var jpeg, out var detectedFormat, out var width,
                out var height, out var failureReason);

            Assert.IsTrue(ok, failureReason);
            Assert.AreEqual("jpeg", detectedFormat);
            Assert.AreEqual(2, width);
            Assert.AreEqual(1, height);
            Assert.IsTrue(JpegFrameValidator.TryGetDimensions(jpeg,
                out var outputWidth, out var outputHeight));
            Assert.AreEqual(2, outputWidth);
            Assert.AreEqual(1, outputHeight);
        }

        [TestMethod]
        public void SnapshotFileReader_DoesNotTreatHalfWrittenImageAsValidFrame()
        {
            var source = CreateImageBytes(ImageFormat.Png);
            var halfWritten = new byte[Math.Max(8, source.Length / 2)];
            Buffer.BlockCopy(source, 0, halfWritten, 0, halfWritten.Length);
            var path = Path.Combine(Path.GetTempPath(),
                "HZCYKJTHardWare_PlateFrameTest_" + Guid.NewGuid().ToString("N") + ".jpg");

            try
            {
                File.WriteAllBytes(path, halfWritten);
                Assert.IsTrue(SnapshotFileReader.TryReadStable(path, 8 * 1024 * 1024,
                    100, () => false, out var raw, out var fileBytes,
                    out var readFailure), readFailure);
                Assert.AreEqual(halfWritten.Length, fileBytes);
                Assert.IsFalse(SnapshotImageNormalizer.TryNormalizeToJpeg(raw,
                    out var unusedJpeg, out var unusedFormat, out var unusedWidth,
                    out var unusedHeight, out var normalizeFailure), normalizeFailure);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void LatestPlateFrameCache_RetainsLastGoodFrameUntilCleared()
        {
            var cache = new LatestPlateFrameCache();
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            cache.Publish(jpeg, 2, 1, "jpeg", DateTime.UtcNow);

            jpeg[0] = 0;
            Assert.IsTrue(cache.TryGet(out var first));
            Assert.AreEqual(0xFF, first.Jpeg[0]);
            var firstSequence = first.Sequence;

            Assert.IsTrue(cache.TryGet(out var retained));
            Assert.AreEqual(firstSequence, retained.Sequence);
            Assert.AreEqual("jpeg", retained.Format);
            Assert.AreEqual(2, retained.Width);
            Assert.AreEqual(1, retained.Height);
        }

        [TestMethod]
        public void LatestPlateFrameFreshness_UsesBoundedAgeAndDimensions()
        {
            var now = DateTime.UtcNow;
            var fresh = new LatestPlateFrameSnapshot(
                new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, 2, 1,
                "jpeg", 1, now.AddMilliseconds(-100));
            var stale = new LatestPlateFrameSnapshot(
                new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, 2, 1,
                "jpeg", 2, now.AddMilliseconds(-1001));

            Assert.IsTrue(PreviewManager.IsLatestPlateFrameFresh(fresh, now));
            Assert.IsFalse(PreviewManager.IsLatestPlateFrameFresh(stale, now));
            Assert.AreEqual(1000, VlcPreviewController.LatestPlateFrameMaxAgeMs);
        }

        [TestMethod]
        public void LatestPlateFrameRetry_IsSingleAndWithinBoundedBudget()
        {
            Assert.AreEqual(1, VlcPreviewController.LatestPlateFrameMaxRetries);
            Assert.AreEqual(75, VlcPreviewController.LatestPlateFrameRetryDelayMs);
            Assert.IsTrue(VlcPreviewController.LatestPlateFrameRetryBudgetMs >=
                          VlcPreviewController.LatestPlateFrameRefreshTimeoutMs +
                          VlcPreviewController.LatestPlateFrameRetryDelayMs);
            Assert.IsTrue(VlcPreviewController.LatestPlateFrameRetryBudgetMs <= 1200);
        }

        [TestMethod]
        public void DllBinaryResponse_PreservesAndSanitizesFrameMetadataHeaders()
        {
            var headers = new Dictionary<string, string>
            {
                ["X-HZCY-Capture-Request-Id"] = "CAPTURE-1\r\nInjected: no",
                ["X-HZCY-Frame-Width"] = "1920"
            };

            using (var response = DllBinaryResponse.Binary(
                new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, "image/jpeg", headers))
            {
                Assert.AreEqual("CAPTURE-1  Injected: no",
                    response.Headers["X-HZCY-Capture-Request-Id"]);
                Assert.AreEqual("1920", response.Headers["X-HZCY-Frame-Width"]);
            }
        }

        [TestMethod]
        public void JpegFrameValidator_ReadsSofDimensions()
        {
            var jpeg = new byte[]
            {
                0xFF, 0xD8,
                0xFF, 0xE0, 0x00, 0x02,
                0xFF, 0xC0, 0x00, 0x0B,
                0x08, 0x04, 0x38, 0x07, 0x80, 0x03, 0x01, 0x11, 0x00,
                0xFF, 0xD9
            };

            Assert.IsTrue(JpegFrameValidator.TryGetDimensions(jpeg,
                out var width, out var height));
            Assert.AreEqual(1920, width);
            Assert.AreEqual(1080, height);
        }

        [TestMethod]
        public void JpegFrameValidator_RejectsNonJpeg()
        {
            Assert.IsFalse(JpegFrameValidator.TryGetDimensions(
                new byte[] { 0x01, 0x02, 0x03, 0x04 },
                out var width, out var height));
            Assert.AreEqual(0, width);
            Assert.AreEqual(0, height);
        }

        [TestMethod]
        public void JpegFrameValidator_RejectsZeroDimensions()
        {
            var jpeg = new byte[]
            {
                0xFF, 0xD8,
                0xFF, 0xC0, 0x00, 0x0B,
                0x08, 0x00, 0x00, 0x07, 0x80, 0x03, 0x01, 0x11, 0x00,
                0xFF, 0xD9
            };

            Assert.IsFalse(JpegFrameValidator.TryGetDimensions(jpeg,
                out var width, out var height));
            Assert.AreEqual(0, width);
            Assert.AreEqual(0, height);
        }

        [TestMethod]
        public void LatestPlateFrameRoutes_NormalizePlateCaseAndRequestTarget()
        {
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/cj/latest-frame?trace=1"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/CJ/latest-frame"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "  /preview/plate/Cj/latest-frame/?trace=1  "));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/rj2/latest-frame"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "http://127.0.0.1:8089/preview/plate/RJ2/latest-frame"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/rj3/latest-frame"));
            Assert.IsTrue(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/RJ3/latest%2Dframe"));
            Assert.IsFalse(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/cj/start"));
            Assert.IsFalse(DllCommandHandler.IsLatestPlateFramePath(
                "/preview/plate/unknown/latest-frame"));
        }

        private static byte[] CreateImageBytes(ImageFormat format)
        {
            using (var bitmap = new Bitmap(2, 1))
            {
                bitmap.SetPixel(0, 0, Color.Red);
                bitmap.SetPixel(1, 0, Color.Blue);
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, format);
                    return stream.ToArray();
                }
            }
        }
    }
}
