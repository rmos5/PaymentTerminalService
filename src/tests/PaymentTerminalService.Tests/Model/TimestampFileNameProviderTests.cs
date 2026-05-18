using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class TimestampFileNameProviderTests
    {
        private TimestampFileNameProvider provider;

        [TestInitialize]
        public void Setup()
        {
            provider = new TimestampFileNameProvider();
        }

        // GetOrCreateSessionName

        [TestMethod]
        public void GetOrCreateSessionName_WithExistingName_ReturnsSameName()
        {
            var result = provider.GetOrCreateSessionName("my-session");

            Assert.AreEqual("my-session", result);
        }

        [TestMethod]
        public void GetOrCreateSessionName_WithNull_ReturnsNewTimestampFormattedName()
        {
            var before = DateTimeOffset.UtcNow;
            var result = provider.GetOrCreateSessionName(null);
            var after = DateTimeOffset.UtcNow;

            Assert.IsNotNull(result);
            Assert.IsTrue(DateTimeOffset.TryParseExact(result, TimestampFileNameProvider.TimestampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed));
            Assert.IsTrue(parsed >= before.AddMilliseconds(-1) && parsed <= after.AddMilliseconds(1));
        }

        [TestMethod]
        public void GetOrCreateSessionName_WithWhitespace_ReturnsNewTimestampFormattedName()
        {
            var result = provider.GetOrCreateSessionName("   ");

            Assert.IsTrue(DateTimeOffset.TryParseExact(result, TimestampFileNameProvider.TimestampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _));
        }

        // GetOngoingSessionFileName

        [TestMethod]
        public void GetOngoingSessionFileName_ValidName_ReturnsWithPrefixAndExtension()
        {
            var result = provider.GetOngoingSessionFileName("my-session");

            Assert.AreEqual("_my-session.ptss", result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetOngoingSessionFileName_NullName_Throws()
        {
            provider.GetOngoingSessionFileName(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetOngoingSessionFileName_WhitespaceName_Throws()
        {
            provider.GetOngoingSessionFileName("  ");
        }

        // GetCompletedSessionFileName

        [TestMethod]
        public void GetCompletedSessionFileName_ValidName_ReturnsWithExtensionOnly()
        {
            var result = provider.GetCompletedSessionFileName("my-session");

            Assert.AreEqual("my-session.ptss", result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetCompletedSessionFileName_NullName_Throws()
        {
            provider.GetCompletedSessionFileName(null);
        }

        // GetFailedSessionFileName

        [TestMethod]
        public void GetFailedSessionFileName_ValidName_ReturnsWithFailedSuffixAndExtension()
        {
            var result = provider.GetFailedSessionFileName("my-session");

            Assert.AreEqual("my-session.failed.ptss", result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetFailedSessionFileName_NullName_Throws()
        {
            provider.GetFailedSessionFileName(null);
        }

        // GetConfirmedSessionFileName

        [TestMethod]
        public void GetConfirmedSessionFileName_ValidName_ReturnsWithConfirmedSuffixAndExtension()
        {
            var result = provider.GetConfirmedSessionFileName("my-session");

            Assert.AreEqual("my-session.confirmed.ptss", result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetConfirmedSessionFileName_NullName_Throws()
        {
            provider.GetConfirmedSessionFileName(null);
        }

        // GetOrphanSessionFileName

        [TestMethod]
        public void GetOrphanSessionFileName_ValidName_ReturnsWithOrphanSuffixAndExtension()
        {
            var result = provider.GetOrphanSessionFileName("my-session");

            Assert.AreEqual("my-session.orphan.ptss", result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetOrphanSessionFileName_NullName_Throws()
        {
            provider.GetOrphanSessionFileName(null);
        }
    }
}