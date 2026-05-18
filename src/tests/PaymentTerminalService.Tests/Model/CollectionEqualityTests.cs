using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class CollectionEqualityTests
    {
        // DictionaryEquals

        [TestMethod]
        public void DictionaryEquals_SameReference_ReturnsTrue()
        {
            var source = new Dictionary<string, object> { ["a"] = 1 };

            Assert.IsTrue(source.DictionaryEquals(source));
        }

        [TestMethod]
        public void DictionaryEquals_EqualContent_ReturnsTrue()
        {
            var source = new Dictionary<string, object> { ["a"] = 1, ["b"] = "hello" };
            var other  = new Dictionary<string, object> { ["b"] = "hello", ["a"] = 1 };

            Assert.IsTrue(source.DictionaryEquals(other));
        }

        [TestMethod]
        public void DictionaryEquals_DifferentValues_ReturnsFalse()
        {
            var source = new Dictionary<string, object> { ["a"] = 1 };
            var other  = new Dictionary<string, object> { ["a"] = 2 };

            Assert.IsFalse(source.DictionaryEquals(other));
        }

        [TestMethod]
        public void DictionaryEquals_DifferentKeys_ReturnsFalse()
        {
            var source = new Dictionary<string, object> { ["a"] = 1 };
            var other  = new Dictionary<string, object> { ["b"] = 1 };

            Assert.IsFalse(source.DictionaryEquals(other));
        }

        [TestMethod]
        public void DictionaryEquals_DifferentCounts_ReturnsFalse()
        {
            var source = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
            var other  = new Dictionary<string, object> { ["a"] = 1 };

            Assert.IsFalse(source.DictionaryEquals(other));
        }

        [TestMethod]
        public void DictionaryEquals_SourceNull_ReturnsFalse()
        {
            IDictionary<string, object> source = null;
            var other = new Dictionary<string, object> { ["a"] = 1 };

            Assert.IsFalse(source.DictionaryEquals(other));
        }

        [TestMethod]
        public void DictionaryEquals_OtherNull_ReturnsFalse()
        {
            var source = new Dictionary<string, object> { ["a"] = 1 };

            Assert.IsFalse(source.DictionaryEquals(null));
        }

        [TestMethod]
        public void DictionaryEquals_BothEmpty_ReturnsTrue()
        {
            var source = new Dictionary<string, object>();
            var other  = new Dictionary<string, object>();

            Assert.IsTrue(source.DictionaryEquals(other));
        }

        // GetDictionaryHashCode

        [TestMethod]
        public void GetDictionaryHashCode_NullDictionary_ReturnsZero()
        {
            var result = CollectionEquality.GetDictionaryHashCode(null);

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GetDictionaryHashCode_EmptyDictionary_ReturnsZero()
        {
            var result = CollectionEquality.GetDictionaryHashCode(new Dictionary<string, object>());

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GetDictionaryHashCode_EqualDictionaries_ReturnSameHashCode()
        {
            var first  = new Dictionary<string, object> { ["a"] = 1, ["b"] = "x" };
            var second = new Dictionary<string, object> { ["b"] = "x", ["a"] = 1 };

            Assert.AreEqual(
                CollectionEquality.GetDictionaryHashCode(first),
                CollectionEquality.GetDictionaryHashCode(second));
        }

        [TestMethod]
        public void GetDictionaryHashCode_DifferentDictionaries_ReturnDifferentHashCodes()
        {
            var first  = new Dictionary<string, object> { ["a"] = 1 };
            var second = new Dictionary<string, object> { ["a"] = 2 };

            Assert.AreNotEqual(
                CollectionEquality.GetDictionaryHashCode(first),
                CollectionEquality.GetDictionaryHashCode(second));
        }

        // CollectionEquals

        [TestMethod]
        public void CollectionEquals_SameReference_ReturnsTrue()
        {
            var source = new List<string> { "a", "b" };

            Assert.IsTrue(source.CollectionEquals(source));
        }

        [TestMethod]
        public void CollectionEquals_EqualSequence_ReturnsTrue()
        {
            var source = new List<string> { "a", "b" };
            var other  = new List<string> { "a", "b" };

            Assert.IsTrue(source.CollectionEquals(other));
        }

        [TestMethod]
        public void CollectionEquals_DifferentOrder_ReturnsFalse()
        {
            var source = new List<string> { "a", "b" };
            var other  = new List<string> { "b", "a" };

            Assert.IsFalse(source.CollectionEquals(other));
        }

        [TestMethod]
        public void CollectionEquals_DifferentCount_ReturnsFalse()
        {
            var source = new List<string> { "a", "b" };
            var other  = new List<string> { "a" };

            Assert.IsFalse(source.CollectionEquals(other));
        }

        [TestMethod]
        public void CollectionEquals_SourceNull_ReturnsFalse()
        {
            ICollection<string> source = null;
            var other = new List<string> { "a" };

            Assert.IsFalse(source.CollectionEquals(other));
        }

        [TestMethod]
        public void CollectionEquals_OtherNull_ReturnsFalse()
        {
            var source = new List<string> { "a" };

            Assert.IsFalse(source.CollectionEquals(null));
        }

        [TestMethod]
        public void CollectionEquals_BothEmpty_ReturnsTrue()
        {
            Assert.IsTrue(new List<string>().CollectionEquals(new List<string>()));
        }

        // GetCollectionHashCode

        [TestMethod]
        public void GetCollectionHashCode_NullCollection_ReturnsZero()
        {
            Assert.AreEqual(0, CollectionEquality.GetCollectionHashCode<string>(null));
        }

        [TestMethod]
        public void GetCollectionHashCode_EmptyCollection_ReturnsZero()
        {
            Assert.AreEqual(0, CollectionEquality.GetCollectionHashCode(new List<string>()));
        }

        [TestMethod]
        public void GetCollectionHashCode_EqualCollections_ReturnSameHashCode()
        {
            var first  = new List<string> { "a", "b" };
            var second = new List<string> { "a", "b" };

            Assert.AreEqual(
                CollectionEquality.GetCollectionHashCode(first),
                CollectionEquality.GetCollectionHashCode(second));
        }

        [TestMethod]
        public void GetCollectionHashCode_DifferentOrder_ReturnDifferentHashCodes()
        {
            var first  = new List<string> { "a", "b" };
            var second = new List<string> { "b", "a" };

            Assert.AreNotEqual(
                CollectionEquality.GetCollectionHashCode(first),
                CollectionEquality.GetCollectionHashCode(second));
        }
    }
}