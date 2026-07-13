using System;
using System.IO;
using System.Linq;
using HZCYKJTHardWare.Proxy.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Storage
{
    [TestClass]
    public class FileSaverTests
    {
        [TestMethod]
        public void SaveBase64ImageToFile_ReplacesExistingFileWithoutTempResidue()
        {
            var directory = CreateTempDirectory();
            try
            {
                var target = Path.Combine(directory, "face.jpg");
                File.WriteAllBytes(target, new byte[] { 1, 2, 3 });
                var expected = new byte[] { 9, 8, 7, 6, 5 };

                var result = FileSaver.SaveBase64ImageToFile(
                    Convert.ToBase64String(expected), target);

                Assert.AreEqual(target, result);
                CollectionAssert.AreEqual(expected, File.ReadAllBytes(target));
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void SaveBase64ImageToFile_InvalidBase64PreservesExistingFile()
        {
            var directory = CreateTempDirectory();
            try
            {
                var target = Path.Combine(directory, "face.jpg");
                var original = new byte[] { 4, 3, 2, 1 };
                File.WriteAllBytes(target, original);

                var result = FileSaver.SaveBase64ImageToFile("not-base64", target);

                Assert.AreEqual(string.Empty, result);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(target));
                Assert.IsFalse(Directory.EnumerateFiles(directory)
                    .Any(path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void SaveRawGrayscaleAsBmpToFile_ReplacesExistingFile()
        {
            var directory = CreateTempDirectory();
            try
            {
                var target = Path.Combine(directory, "fingerprint.bmp");
                File.WriteAllBytes(target, new byte[] { 1, 2, 3 });
                var pixels = new byte[] { 0, 64, 128, 255 };

                var result = FileSaver.SaveRawGrayscaleAsBmpToFile(
                    Convert.ToBase64String(pixels), target, 2, 2);

                Assert.AreEqual(target, result);
                var actual = File.ReadAllBytes(target);
                Assert.IsTrue(actual.Length > pixels.Length);
                Assert.AreEqual((byte)'B', actual[0]);
                Assert.AreEqual((byte)'M', actual[1]);
                Assert.AreEqual(2, BitConverter.ToInt32(actual, 18));
                Assert.AreEqual(2, BitConverter.ToInt32(actual, 22));
                CollectionAssert.AreEqual(
                    new byte[] { 128, 255, 0, 0, 0, 64, 0, 0 },
                    actual.Skip(1078).ToArray());
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void SaveRawGrayscaleAsBmpToFile_LargeImagePreservesBottomUpRows()
        {
            var directory = CreateTempDirectory();
            try
            {
                const int width = 352;
                const int height = 544;
                const int pixelOffset = 1078;
                var target = Path.Combine(directory, "fingerprint.bmp");
                var pixels = Enumerable.Range(0, width * height)
                    .Select(index => (byte)(index % 251))
                    .ToArray();

                var result = FileSaver.SaveRawGrayscaleAsBmpToFile(
                    Convert.ToBase64String(pixels), target, width, height);

                Assert.AreEqual(target, result);
                var actual = File.ReadAllBytes(target);
                Assert.AreEqual(pixelOffset + pixels.Length, actual.Length);
                CollectionAssert.AreEqual(
                    pixels.Skip((height - 1) * width).Take(width).ToArray(),
                    actual.Skip(pixelOffset).Take(width).ToArray());
                CollectionAssert.AreEqual(
                    pixels.Take(width).ToArray(),
                    actual.Skip(pixelOffset + ((height - 1) * width))
                        .Take(width).ToArray());
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void SaveRawGrayscaleAsBmpToFile_InvalidBase64PreservesExistingFile()
        {
            var directory = CreateTempDirectory();
            try
            {
                var target = Path.Combine(directory, "fingerprint.bmp");
                var original = new byte[] { 7, 6, 5, 4 };
                File.WriteAllBytes(target, original);

                var result = FileSaver.SaveRawGrayscaleAsBmpToFile(
                    "not-base64", target, 352, 544);

                Assert.AreEqual(string.Empty, result);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(target));
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(),
                "HZCYKJTHardWare_FileSaverTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
                // Test cleanup must not hide the assertion result.
            }
        }
    }
}
