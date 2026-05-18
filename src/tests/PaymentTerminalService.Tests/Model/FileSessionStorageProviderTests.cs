using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class FileSessionStorageProviderTests
    {
        private string tempDirectory;

        [TestInitialize]
        public void Setup()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullDirectory_Throws()
        {
            new FileSessionStorageProvider(null, new TimestampFileNameProvider());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WhitespaceDirectory_Throws()
        {
            new FileSessionStorageProvider("   ", new TimestampFileNameProvider());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullNameProvider_Throws()
        {
            new FileSessionStorageProvider(tempDirectory, null);
        }

        [TestMethod]
        public void Constructor_ValidArguments_CreatesDirectory()
        {
            new FileSessionStorageProvider(tempDirectory, new TimestampFileNameProvider());

            Assert.IsTrue(Directory.Exists(tempDirectory));
        }

        [TestMethod]
        public void Constructor_ValidArguments_MarksDirectoryAsHidden()
        {
            new FileSessionStorageProvider(tempDirectory, new TimestampFileNameProvider());

            var attributes = File.GetAttributes(tempDirectory);
            Assert.IsTrue((attributes & FileAttributes.Hidden) != 0);
        }
    }
}