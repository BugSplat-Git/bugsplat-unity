using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System.Collections.Generic;

namespace BugSplatUnity.RuntimeTests.Util
{
    public class AttributesJsonTests
    {
        [Test]
        public void Serialize_WhenNull_ShouldReturnEmptyObject()
        {
            Assert.AreEqual("{}", AttributesJson.Serialize(null));
        }

        [Test]
        public void Serialize_WhenEmpty_ShouldReturnEmptyObject()
        {
            Assert.AreEqual("{}", AttributesJson.Serialize(new Dictionary<string, string>()));
        }

        [Test]
        public void Serialize_WithValues_ShouldReturnObjectWithEachPair()
        {
            var attributes = new Dictionary<string, string>
            {
                { "level", "3" },
                { "player", "fred" }
            };

            Assert.AreEqual(@"{""level"":""3"",""player"":""fred""}", AttributesJson.Serialize(attributes));
        }

        [Test]
        public void Serialize_WithEmptyValue_ShouldReturnEmptyString()
        {
            var attributes = new Dictionary<string, string> { { "empty", "" } };

            Assert.AreEqual(@"{""empty"":""""}", AttributesJson.Serialize(attributes));
        }

        [Test]
        public void Serialize_WithNullValue_ShouldReturnJsonNull()
        {
            var attributes = new Dictionary<string, string> { { "missing", null } };

            Assert.AreEqual(@"{""missing"":null}", AttributesJson.Serialize(attributes));
        }

        [Test]
        public void Serialize_WithQuotesAndBackslashes_ShouldEscapeThem()
        {
            var attributes = new Dictionary<string, string>
            {
                { "say \"hi\"", "C:\\Games\\\"My Game\"" }
            };

            Assert.AreEqual(@"{""say \""hi\"""":""C:\\Games\\\""My Game\""""}", AttributesJson.Serialize(attributes));
        }

        [Test]
        public void Serialize_WithControlCharacters_ShouldEscapeThem()
        {
            var attributes = new Dictionary<string, string>
            {
                { "log", "a\nb\r\tc\b\f\u0001\u001f\u007f" }
            };

            Assert.AreEqual(@"{""log"":""a\nb\r\tc\b\f\u0001\u001f\u007f""}", AttributesJson.Serialize(attributes));
        }

        [Test]
        public void Serialize_WithNonAsciiCharacters_ShouldEscapeThemAsUtf16CodeUnits()
        {
            var attributes = new Dictionary<string, string>
            {
                { "caf\u00e9", "\u2713 \U0001F600 \u2028" }
            };

            Assert.AreEqual(@"{""caf\u00e9"":""\u2713 \ud83d\ude00 \u2028""}", AttributesJson.Serialize(attributes));
        }

        [Test]
        public void Serialize_WithLoneSurrogate_ShouldEscapeItWithoutThrowing()
        {
            var attributes = new Dictionary<string, string> { { "broken", "\ud83d" } };

            Assert.AreEqual(@"{""broken"":""\ud83d""}", AttributesJson.Serialize(attributes));
        }
    }
}
