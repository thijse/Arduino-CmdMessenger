using System;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Unit tests for the static BinaryConverter class.
    ///
    /// BinaryConverter converts typed values to ISO-8859-1 escape-encoded strings
    /// (suitable for embedding raw binary payloads in the CmdMessenger wire protocol)
    /// and back again.
    ///
    /// Coverage:
    ///   - Round-trips for float, double, int, uint, short, ushort, byte
    ///   - Null / empty input returns null for all To*() methods
    ///   - EscapedStringToBytes / StringToBytes produce non-empty output for non-empty input
    ///   - Binary payloads whose raw bytes coincide with protocol special characters
    ///     (field separator ',', command separator ';', escape char '/', null '\0')
    ///     survive the Escape → Unescape round-trip intact
    /// </summary>
    public class BinaryConverterTests
    {
        public BinaryConverterTests()
        {
            // Ensure default protocol separators for all tests
            Escaping.EscapeChars(',', ';', '/');
        }

        // ------------------------------------------------------------------ float

        [Theory]
        [InlineData(0f)]
        [InlineData(1f)]
        [InlineData(-1f)]
        [InlineData(3.14159265f)]
        [InlineData(float.MaxValue)]
        [InlineData(float.MinValue)]
        [InlineData(float.Epsilon)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void ToString_ToFloat_RoundTrip(float value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToFloat(encoded);
            Assert.NotNull(decoded);

            if (float.IsNaN(value))
                Assert.True(float.IsNaN(decoded.Value));
            else
                Assert.Equal(value, decoded.Value);
        }

        // ------------------------------------------------------------------ double

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(3.141592653589793)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(double.Epsilon)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ToString_ToDouble_RoundTrip(double value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToDouble(encoded);
            Assert.NotNull(decoded);

            if (double.IsNaN(value))
                Assert.True(double.IsNaN(decoded.Value));
            else
                Assert.Equal(value, decoded.Value);
        }

        // ------------------------------------------------------------------ int / uint

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [InlineData(12345678)]
        public void ToString_ToInt32_RoundTrip(int value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToInt32(encoded);
            Assert.NotNull(decoded);
            Assert.Equal(value, decoded.Value);
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(uint.MaxValue)]
        [InlineData(2147483648u)]
        public void ToString_ToUInt32_RoundTrip(uint value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToUInt32(encoded);
            Assert.NotNull(decoded);
            Assert.Equal(value, decoded.Value);
        }

        // ------------------------------------------------------------------ short / ushort

        [Theory]
        [InlineData((short)0)]
        [InlineData((short)1)]
        [InlineData((short)-1)]
        [InlineData(short.MaxValue)]
        [InlineData(short.MinValue)]
        public void ToString_ToInt16_RoundTrip(short value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToInt16(encoded);
            Assert.NotNull(decoded);
            Assert.Equal(value, decoded.Value);
        }

        [Theory]
        [InlineData((ushort)0)]
        [InlineData((ushort)1)]
        [InlineData(ushort.MaxValue)]
        [InlineData((ushort)1000)]
        public void ToString_ToUInt16_RoundTrip(ushort value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToUInt16(encoded);
            Assert.NotNull(decoded);
            Assert.Equal(value, decoded.Value);
        }

        // ------------------------------------------------------------------ byte

        [Theory]
        [InlineData((byte)0)]
        [InlineData((byte)1)]
        [InlineData((byte)255)]
        [InlineData((byte)44)]   // ASCII ','
        [InlineData((byte)59)]   // ASCII ';'
        [InlineData((byte)47)]   // ASCII '/'
        public void ToString_ToByte_RoundTrip(byte value)
        {
            var encoded = BinaryConverter.ToString(value);
            Assert.NotNull(encoded);

            var decoded = BinaryConverter.ToByte(encoded);
            Assert.NotNull(decoded);
            Assert.Equal(value, decoded.Value);
        }

        // ------------------------------------------------------------------ null / empty input

        [Fact]
        public void ToFloat_NullInput_ReturnsNull()
        {
            Assert.Null(BinaryConverter.ToFloat(null));
        }

        [Fact]
        public void ToFloat_EmptyInput_ReturnsNull()
        {
            // Empty string decodes to zero bytes — too short for a float (needs 4)
            Assert.Null(BinaryConverter.ToFloat(string.Empty));
        }

        [Fact]
        public void ToInt32_NullInput_ReturnsNull()
        {
            Assert.Null(BinaryConverter.ToInt32(null));
        }

        [Fact]
        public void ToInt32_EmptyInput_ReturnsNull()
        {
            Assert.Null(BinaryConverter.ToInt32(string.Empty));
        }

        [Fact]
        public void ToInt16_NullInput_ReturnsNull()
        {
            Assert.Null(BinaryConverter.ToInt16(null));
        }

        [Fact]
        public void ToByte_NullInput_ReturnsNull()
        {
            Assert.Null(BinaryConverter.ToByte(null));
        }

        // ------------------------------------------------------------------ EscapedStringToBytes / StringToBytes

        [Fact]
        public void EscapedStringToBytes_NonEmptyInput_ReturnsNonEmpty()
        {
            var bytes = BinaryConverter.EscapedStringToBytes("A");
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void StringToBytes_NonEmptyInput_ReturnsNonEmpty()
        {
            var bytes = BinaryConverter.StringToBytes("hello");
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void EscapedStringToBytes_EscapedComma_DecodesCorrectly()
        {
            // "/," should decode to a single byte equal to ','  (0x2C)
            var bytes = BinaryConverter.EscapedStringToBytes("/,");
            Assert.NotNull(bytes);
            Assert.Single(bytes);
            Assert.Equal((byte)',', bytes[0]);
        }

        [Fact]
        public void EscapedStringToBytes_EscapedSemicolon_DecodesCorrectly()
        {
            var bytes = BinaryConverter.EscapedStringToBytes("/;");
            Assert.NotNull(bytes);
            Assert.Single(bytes);
            Assert.Equal((byte)';', bytes[0]);
        }

        // ------------------------------------------------------------------ Special-byte collisions

        // Float values chosen so that at least one raw byte equals a protocol special char:
        //   0x2C = ','    0x3B = ';'    0x2F = '/'    0x00 = '\0'
        // The encoding escapes those bytes, so the round-trip must still be exact.

        [Fact]
        public void Float_WithCommaByteInRepresentation_RoundTrips()
        {
            // Force a float whose bytes contain 0x2C  (',')
            // Build it directly from bytes: [0x2C, 0x2C, 0x2C, 0x2C]
            byte[] raw = { 0x2C, 0x2C, 0x2C, 0x2C };
            float original = BitConverter.ToSingle(raw, 0);

            var encoded = BinaryConverter.ToString(original);
            var decoded = BinaryConverter.ToFloat(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original, decoded.Value);
        }

        [Fact]
        public void Float_WithSemicolonByteInRepresentation_RoundTrips()
        {
            byte[] raw = { 0x3B, 0x3B, 0x3B, 0x3B };
            float original = BitConverter.ToSingle(raw, 0);

            var encoded = BinaryConverter.ToString(original);
            var decoded = BinaryConverter.ToFloat(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original, decoded.Value);
        }

        [Fact]
        public void Float_WithSlashByteInRepresentation_RoundTrips()
        {
            byte[] raw = { 0x2F, 0x2F, 0x2F, 0x2F };
            float original = BitConverter.ToSingle(raw, 0);

            var encoded = BinaryConverter.ToString(original);
            var decoded = BinaryConverter.ToFloat(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original, decoded.Value);
        }

        [Fact]
        public void Float_WithNullByteInRepresentation_RoundTrips()
        {
            byte[] raw = { 0x00, 0x00, 0x00, 0x00 };   // 0.0f
            float original = BitConverter.ToSingle(raw, 0);

            var encoded = BinaryConverter.ToString(original);
            var decoded = BinaryConverter.ToFloat(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original, decoded.Value);
        }

        [Fact]
        public void Int32_WithAllSpecialBytes_RoundTrips()
        {
            // Build an Int32 whose four bytes are all protocol special chars
            byte[] raw = { (byte)',', (byte)';', (byte)'/', 0x00 };
            int original = BitConverter.ToInt32(raw, 0);

            var encoded = BinaryConverter.ToString(original);
            var decoded = BinaryConverter.ToInt32(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original, decoded.Value);
        }
    }
}
