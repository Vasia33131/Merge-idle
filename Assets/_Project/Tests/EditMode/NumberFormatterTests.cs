using System;
using CatMerge.Core;
using NUnit.Framework;

namespace CatMerge.Tests
{
    public class NumberFormatterTests
    {
        [TestCase(0, "0")]
        [TestCase(7.5, "7.5")]
        [TestCase(2.66, "2.6")]
        [TestCase(99.99, "99.9")]
        [TestCase(950.7, "950")]
        [TestCase(1000, "1K")]
        [TestCase(1234, "1.2K")]
        [TestCase(12345, "12.3K")]
        [TestCase(123456, "123K")]
        [TestCase(999999, "999K")]
        [TestCase(1e6, "1M")]
        [TestCase(3.4e6, "3.4M")]
        [TestCase(5.6e9, "5.6B")]
        [TestCase(7.8e12, "7.8T")]
        [TestCase(1e15, "1aa")]
        [TestCase(2.5e18, "2.5ab")]
        [TestCase(1e90, "1az")]
        [TestCase(1e93, "1ba")]
        [TestCase(-1234, "-1.2K")]
        public void Format(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.Format(value));
        }

        [Test]
        public void Format_NeverRoundsUp()
        {
            Assert.AreEqual("9.9", NumberFormatter.Format(9.99));
            Assert.AreEqual("999T", NumberFormatter.Format(999.99e12));
        }

        [Test]
        public void Suffix_LettersAfterTrillion()
        {
            Assert.AreEqual("T", NumberFormatter.Suffix(4));
            Assert.AreEqual("aa", NumberFormatter.Suffix(5));
            Assert.AreEqual("zz", NumberFormatter.Suffix(5 + 26 * 26 - 1));
            Assert.IsNull(NumberFormatter.Suffix(5 + 26 * 26));
        }

        [Test]
        public void FromLog10_BeyondDoubleUsesLetters()
        {
            // 10^807 is group 269 -> letter pair #264 -> "ke".
            Assert.AreEqual("2ke", NumberFormatter.FromLog10(807 + Math.Log10(2)));
        }

        [Test]
        public void FromLog10_BeyondLettersUsesExponent()
        {
            Assert.AreEqual("2e3000", NumberFormatter.FromLog10(3000 + Math.Log10(2)));
        }
    }
}
