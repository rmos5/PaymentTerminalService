using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class DictionaryPropertyExtensionsTests
    {
        // TryGetProperty

        [TestMethod]
        public void TryGetProperty_ExistingTypedValue_ReturnsTrueAndValue()
        {
            var source = new Dictionary<string, object> { ["key"] = 42 };

            var found = source.TryGetProperty<int>("key", out var value);

            Assert.IsTrue(found);
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void TryGetProperty_MissingKey_ReturnsFalse()
        {
            var source = new Dictionary<string, object>();

            var found = source.TryGetProperty<int>("missing", out var value);

            Assert.IsFalse(found);
            Assert.AreEqual(default(int), value);
        }

        [TestMethod]
        public void TryGetProperty_NullValueForReferenceType_ReturnsTrueWithNull()
        {
            var source = new Dictionary<string, object> { ["key"] = null };

            var found = source.TryGetProperty<string>("key", out var value);

            Assert.IsTrue(found);
            Assert.IsNull(value);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TryGetProperty_NullValueForValueType_Throws()
        {
            var source = new Dictionary<string, object> { ["key"] = null };

            source.TryGetProperty<int>("key", out _);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TryGetProperty_NullSource_Throws()
        {
            IDictionary<string, object> source = null;

            source.TryGetProperty<int>("key", out _);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TryGetProperty_WhitespacePropertyName_Throws()
        {
            var source = new Dictionary<string, object>();

            source.TryGetProperty<int>("  ", out _);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TryGetProperty_IncompatibleType_Throws()
        {
            var source = new Dictionary<string, object> { ["key"] = "not-an-int" };

            source.TryGetProperty<int>("key", out _);
        }

        // GetPropertyOrDefault

        [TestMethod]
        public void GetPropertyOrDefault_ExistingKey_ReturnsValue()
        {
            var source = new Dictionary<string, object> { ["key"] = 99 };

            var result = source.GetPropertyOrDefault<int>("key");

            Assert.AreEqual(99, result);
        }

        [TestMethod]
        public void GetPropertyOrDefault_MissingKey_ReturnsDefault()
        {
            var source = new Dictionary<string, object>();

            var result = source.GetPropertyOrDefault<int>("missing", defaultValue: -1);

            Assert.AreEqual(-1, result);
        }

        [TestMethod]
        public void GetPropertyOrDefault_MissingKey_NoDefaultProvided_ReturnsTypeDefault()
        {
            var source = new Dictionary<string, object>();

            var result = source.GetPropertyOrDefault<string>("missing");

            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetPropertyOrDefault_NullSource_Throws()
        {
            IDictionary<string, object> source = null;

            source.GetPropertyOrDefault<int>("key");
        }
    }
}