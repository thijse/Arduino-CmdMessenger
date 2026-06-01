using System;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Tests for the Escaping utility class which handles encoding/decoding of
    /// special characters in the CmdMessenger wire protocol.
    ///
    /// The protocol uses three special characters:
    ///   - Field separator  (default ',') — separates arguments within a command
    ///   - Command separator (default ';') — marks end of a command
    ///   - Escape character  (default '/') — prefixes a literal special char
    ///
    /// These tests verify that:
    ///   1. Each special character is properly escaped on encode
    ///   2. Escape → Unescape is lossless (round-trip identity)
    ///   3. Split respects escaped separators
    ///   4. Remove skips escaped characters
    ///   5. Custom separator sets work identically
    /// </summary>
    public class EscapingTests
    {
        public EscapingTests()
        {
            // Ensure default separators for all tests
            Escaping.EscapeChars(',', ';', '/');
        }

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("", "")]
        [InlineData("no special chars", "no special chars")]
        public void Escape_PlainText_Unchanged(string input, string expected)
        {
            Assert.Equal(expected, Escaping.Escape(input));
        }

        [Fact]
        public void Escape_FieldSeparator_IsEscaped()
        {
            // ',' → '/,'
            Assert.Equal("/,", Escaping.Escape(","));
            Assert.Equal("a/,b", Escaping.Escape("a,b"));
        }

        [Fact]
        public void Escape_CommandSeparator_IsEscaped()
        {
            // ';' → '/;'
            Assert.Equal("/;", Escaping.Escape(";"));
            Assert.Equal("a/;b", Escaping.Escape("a;b"));
        }

        [Fact]
        public void Escape_EscapeCharacter_IsEscaped()
        {
            // '/' → '//'
            Assert.Equal("//", Escaping.Escape("/"));
            Assert.Equal("a//b", Escaping.Escape("a/b"));
        }

        [Fact]
        public void Escape_NullChar_IsEscaped()
        {
            Assert.Equal("/\0", Escaping.Escape("\0"));
        }

        [Fact]
        public void Escape_MultipleSpecialChars()
        {
            // ",;/" → "/,/;//"
            Assert.Equal("/,/;//", Escaping.Escape(",;/"));
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("")]
        [InlineData(",")]
        [InlineData(";")]
        [InlineData("/")]
        [InlineData("\0")]
        [InlineData(",;/\0")]
        [InlineData("a,b;c/d\0e")]
        [InlineData("//,,;;")]
        public void Escape_Unescape_RoundTrip(string original)
        {
            var escaped = Escaping.Escape(original);
            var unescaped = Escaping.Unescape(escaped);
            Assert.Equal(original, unescaped);
        }

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("//", "/")]
        [InlineData("/,", ",")]
        [InlineData("/;", ";")]
        public void Unescape_Values(string input, string expected)
        {
            Assert.Equal(expected, Escaping.Unescape(input));
        }

        [Fact]
        public void Split_BasicFields()
        {
            var parts = Escaping.Split("1,hello,42", ',', '/', StringSplitOptions.None);
            Assert.Equal(new[] { "1", "hello", "42" }, parts);
        }

        [Fact]
        public void Split_EscapedSeparator_NotSplit()
        {
            // "a/,b" should not split on the escaped comma
            var parts = Escaping.Split("a/,b", ',', '/', StringSplitOptions.None);
            Assert.Single(parts);
            Assert.Equal("a/,b", parts[0]);
        }

        [Fact]
        public void Split_EmptyFields_Preserved()
        {
            var parts = Escaping.Split("1,,3", ',', '/', StringSplitOptions.None);
            Assert.Equal(3, parts.Length);
            Assert.Equal("", parts[1]);
        }

        [Fact]
        public void Split_RemoveEmptyEntries()
        {
            var parts = Escaping.Split("1,,3", ',', '/', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, parts.Length);
            Assert.Equal("1", parts[0]);
            Assert.Equal("3", parts[1]);
        }

        [Fact]
        public void Remove_UnescapedChar()
        {
            var result = Escaping.Remove("a,b,c", ',', '/');
            Assert.Equal("abc", result);
        }

        [Fact]
        public void Remove_EscapedChar_Preserved()
        {
            // "a/,b" — the comma is escaped so it should stay
            var result = Escaping.Remove("a/,b", ',', '/');
            Assert.Equal("a/,b", result);
        }

        [Fact]
        public void CustomSeparators_RoundTrip()
        {
            Escaping.EscapeChars('|', '\n', '\\');
            try
            {
                var original = "hello|world\nfoo\\bar";
                var escaped = Escaping.Escape(original);
                var unescaped = Escaping.Unescape(escaped);
                Assert.Equal(original, unescaped);
            }
            finally
            {
                Escaping.EscapeChars(',', ';', '/');
            }
        }

        // --- Edge cases ---

        [Fact]
        public void Escape_NullInput_Throws()
        {
            Assert.ThrowsAny<Exception>(() => Escaping.Escape(null));
        }

        [Fact]
        public void Unescape_NullInput_Throws()
        {
            Assert.ThrowsAny<Exception>(() => Escaping.Unescape(null));
        }

        [Theory]
        [InlineData("café")]
        [InlineData("ñoño")]
        [InlineData("über")]
        [InlineData("\u00FF")] // ÿ — max Latin-1 char
        public void Escape_Latin1Chars_RoundTrip(string input)
        {
            // Characters within ISO-8859-1 range should survive
            var escaped = Escaping.Escape(input);
            var unescaped = Escaping.Unescape(escaped);
            Assert.Equal(input, unescaped);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\r\n")]
        public void Escape_WhitespaceOnly_RoundTrip(string input)
        {
            var escaped = Escaping.Escape(input);
            var unescaped = Escaping.Unescape(escaped);
            Assert.Equal(input, unescaped);
        }

        [Fact]
        public void Escape_LongString_RoundTrip()
        {
            var input = new string('x', 10000) + "," + new string('y', 10000);
            var escaped = Escaping.Escape(input);
            var unescaped = Escaping.Unescape(escaped);
            Assert.Equal(input, unescaped);
        }

        [Fact]
        public void Split_EmptyString_ReturnsSingleEmpty()
        {
            var parts = Escaping.Split("", ',', '/', StringSplitOptions.None);
            Assert.Single(parts);
            Assert.Equal("", parts[0]);
        }

        [Fact]
        public void Split_OnlySeparators_ReturnsEmptyFields()
        {
            var parts = Escaping.Split(",,", ',', '/', StringSplitOptions.None);
            Assert.Equal(3, parts.Length);
            Assert.All(parts, p => Assert.Equal("", p));
        }

        [Fact]
        public void Escape_AllSpecialCharsConsecutive_RoundTrip()
        {
            // Pathological: every character is special
            var input = ",;/\0,;/\0";
            var escaped = Escaping.Escape(input);
            var unescaped = Escaping.Unescape(escaped);
            Assert.Equal(input, unescaped);
        }

        [Fact]
        public void Unescape_TrailingEscapeChar_Handled()
        {
            // Malformed input: escape char at end with nothing following
            // Should not throw — trailing escape is silently dropped
            var result = Escaping.Unescape("hello/");
            Assert.Equal("hello", result);
        }
    }
}
