using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CatMerge.Core;
using NUnit.Framework;

namespace CatMerge.Tests
{
    public class LocalizationTests
    {
        // Unity runs edit mode tests with the project folder as the working directory.
        private const string Dir = "Assets/_Project/Resources/Localization";
        private static readonly string[] Languages = { "en", "ru" };

        private static Dictionary<string, string> Load(string code) =>
            FlatJson.Parse(File.ReadAllText(Path.Combine(Dir, code + ".json"), Encoding.UTF8));

        [Test]
        public void Parse_ReadsStringsAndEscapes()
        {
            var table = FlatJson.Parse("\uFEFF{ \"a\": \"x\\n\\\"y\\\" \\u0416\", \"b\" : \"\" }");

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("x\n\"y\" Ж", table["a"]);
            Assert.AreEqual("", table["b"]);
        }

        [Test]
        public void Parse_EmptyObject() => Assert.AreEqual(0, FlatJson.Parse(" {} ").Count);

        [TestCase("")]
        [TestCase("{\"a\": 1}")]
        [TestCase("{\"a\": \"x\",}")]
        [TestCase("{\"a\": \"x\"} tail")]
        [TestCase("{\"a\": \"x\", \"a\": \"y\"}")]
        [TestCase("{\"a\": \"unterminated}")]
        [TestCase("{\"a\": \"bad \\q escape\"}")]
        public void Parse_RejectsMalformedJson(string json)
        {
            Assert.Throws<FormatException>(() => FlatJson.Parse(json));
        }

        [Test]
        public void Parse_ErrorMentionsLine()
        {
            var e = Assert.Throws<FormatException>(() => FlatJson.Parse("{\n  \"a\": \"x\"\n  \"b\": \"y\"\n}"));
            StringAssert.Contains("line 3", e.Message);
        }

        [Test]
        public void AllLanguages_HaveTheSameKeys()
        {
            var en = Load("en");
            foreach (string code in Languages.Skip(1))
            {
                var other = Load(code);
                CollectionAssert.IsEmpty(en.Keys.Except(other.Keys), $"missing in {code}.json");
                CollectionAssert.IsEmpty(other.Keys.Except(en.Keys), $"only in {code}.json");
            }
        }

        [Test]
        public void AllLanguages_UseTheSamePlaceholders()
        {
            var en = Load("en");
            foreach (string code in Languages.Skip(1))
            {
                var other = Load(code);
                foreach (var pair in en)
                {
                    if (!other.TryGetValue(pair.Key, out string translated)) continue;
                    Assert.AreEqual(Placeholders(pair.Value), Placeholders(translated), $"{code}.json: {pair.Key}");
                }
            }
        }

        [Test]
        public void EveryCat_HasNameAndDescription()
        {
            foreach (string code in Languages)
            {
                var table = Load(code);
                for (int level = 1; level <= 15; level++)
                {
                    foreach (string key in new[] { $"cat_{level:00}_name", $"cat_{level:00}_desc" })
                    {
                        Assert.IsTrue(table.TryGetValue(key, out string text) && text.Trim().Length > 0, $"{code}.json: {key}");
                    }
                }
            }
        }

        [Test]
        public void CatNames_AreUnique()
        {
            foreach (string code in Languages)
            {
                var names = Load(code).Where(p => Regex.IsMatch(p.Key, @"^cat_\d+_name$")).Select(p => p.Value).ToList();
                CollectionAssert.AllItemsAreUnique(names, code);
            }
        }

        private static string Placeholders(string text) =>
            string.Join(",", Regex.Matches(text, @"\{(\d+)[^}]*\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(s => s));
    }
}
