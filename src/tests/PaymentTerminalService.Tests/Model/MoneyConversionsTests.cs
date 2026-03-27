using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class MoneyConversionsTests
    {
        [TestMethod]
        public void MinorUnitsToDecimal_TypicalAmount_ConvertsCorrectly()
        {
            var result = MoneyConversions.MinorUnitsToDecimal(1210);

            Assert.AreEqual(12.10m, result);
        }

        [TestMethod]
        public void MinorUnitsToDecimal_Zero_ReturnsZero()
        {
            var result = MoneyConversions.MinorUnitsToDecimal(0);

            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void MinorUnitsToDecimal_SingleCent_ReturnsOneHundredth()
        {
            var result = MoneyConversions.MinorUnitsToDecimal(1);

            Assert.AreEqual(0.01m, result);
        }

        [TestMethod]
        public void MinorUnitsToDecimal_NegativeAmount_ReturnsNegativeDecimal()
        {
            var result = MoneyConversions.MinorUnitsToDecimal(-500);

            Assert.AreEqual(-5.00m, result);
        }

        [TestMethod]
        public void MinorUnitsToDecimal_RoundAmount_ReturnsWholeNumber()
        {
            var result = MoneyConversions.MinorUnitsToDecimal(10000);

            Assert.AreEqual(100.00m, result);
        }

        [TestMethod]
        public void MinorUnitsToDecimal_WithCurrencyCode_IgnoresCurrencyAndConvertsCorrectly()
        {
            var result = MoneyConversions.MinorUnitsToDecimal(1000, "EUR");

            Assert.AreEqual(10.00m, result);
        }
    }
}