using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Integration tests for clear-text (plain ASCII / decimal) argument round-trips
    /// through the full CmdMessenger receive pipeline using a LoopbackTransport.
    ///
    /// The pattern mirrors the firmware echo model:
    ///   1. The host sends a command via LoopbackTransport.Write().
    ///   2. A "firmware simulator" callback receives the written bytes back through
    ///      SimulateReceive(), echoing them directly into the receive pipeline as if
    ///      the embedded side had sent the same command back.
    ///   3. An echo-listener callback captures the returned command and asserts values.
    ///
    /// Because LoopbackTransport.Write() already fires DataReceived (feeding written bytes
    /// back), for text-mode tests we simply inject a pre-formatted command string via
    /// SimulateReceive() and assert the parsed arguments — matching the pattern used in
    /// CmdMessengerTests.cs.
    ///
    /// Coverage:
    ///   - bool true / false
    ///   - Int16 boundaries: MinValue, -1, 0, 1, MaxValue
    ///   - Int32 boundaries: MinValue, -1, 0, 1, MaxValue
    ///   - float: normal value, NaN, PositiveInfinity, NegativeInfinity
    ///   - double: on Bit16 boards encoded as float32, so float-precision values only
    ///   - string echo
    /// </summary>
    public class ClearTextDataTests : IDisposable
    {
        // Command IDs used in these tests
        private const int CmdEcho = 1;

        private readonly LoopbackTransport _transport;
        private readonly CmdMessenger _messenger;

        public ClearTextDataTests()
        {
            _transport = new LoopbackTransport();
            _messenger = new CmdMessenger(_transport, BoardType.Bit16);
            _transport.Connect();
        }

        public void Dispose()
        {
            _messenger.Dispose();
            _transport.Dispose();
        }

        /// <summary>
        /// Injects a raw command string into the receive pipeline as if it arrived
        /// from the embedded side, then waits for the background queue to process it.
        /// </summary>
        private void SimulateIncoming(string commandString)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(commandString);
            _transport.SimulateReceive(bytes);
            Thread.Sleep(50);
        }

        // ------------------------------------------------------------------ bool

        [Theory]
        [InlineData(true, "1")]
        [InlineData(false, "0")]
        public void Bool_ClearText_RoundTrip(bool expected, string wireValue)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},{wireValue};");

            Assert.NotNull(received);
            Assert.Equal(expected, received.ReadBoolArg());
        }

        // ------------------------------------------------------------------ Int16

        [Theory]
        [InlineData(short.MinValue)]
        [InlineData((short)-1)]
        [InlineData((short)0)]
        [InlineData((short)1)]
        [InlineData(short.MaxValue)]
        public void Int16_ClearText_RoundTrip(short value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},{value.ToString(CultureInfo.InvariantCulture)};");

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadInt16Arg());
        }

        // ------------------------------------------------------------------ Int32

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(int.MaxValue)]
        public void Int32_ClearText_RoundTrip(int value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},{value.ToString(CultureInfo.InvariantCulture)};");

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadInt32Arg());
        }

        // ------------------------------------------------------------------ float

        [Fact]
        public void Float_Normal_ClearText_RoundTrip()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            float value = 1.23456f;
            SimulateIncoming($"{CmdEcho},{value.ToString("R", CultureInfo.InvariantCulture)};");

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadFloatArg(), 5);
        }

        [Fact]
        public void Float_NaN_ClearText_RoundTrip()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},NaN;");

            Assert.NotNull(received);
            Assert.True(float.IsNaN(received.ReadFloatArg()));
        }

        [Fact]
        public void Float_PositiveInfinity_ClearText_RoundTrip()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},Infinity;");

            Assert.NotNull(received);
            Assert.True(float.IsPositiveInfinity(received.ReadFloatArg()));
        }

        [Fact]
        public void Float_NegativeInfinity_ClearText_RoundTrip()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},-Infinity;");

            Assert.NotNull(received);
            Assert.True(float.IsNegativeInfinity(received.ReadFloatArg()));
        }

        // ------------------------------------------------------------------ double
        // On Bit16 boards the library encodes double as float32, so only
        // float-representable values can round-trip losslessly.

        [Theory]
        [InlineData(0.0f)]
        [InlineData(1.0f)]
        [InlineData(-1.0f)]
        [InlineData(3.14159265f)]
        public void Double_Bit16_ClearText_RoundTrip(float valueAsFloat)
        {
            // The messenger is configured as Bit16, so ReadDoubleArg() parses as float.
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            SimulateIncoming($"{CmdEcho},{valueAsFloat.ToString("R", CultureInfo.InvariantCulture)};");

            Assert.NotNull(received);
            double result = received.ReadDoubleArg();
            Assert.Equal((double)valueAsFloat, result, 5);
        }

        // ------------------------------------------------------------------ string

        [Theory]
        [InlineData("hello")]
        [InlineData("")]
        [InlineData("   spaces   ")]
        [InlineData("café")]
        public void String_ClearText_RoundTrip(string value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            // Escape the value for wire encoding (handles any embedded special chars)
            var wireValue = Escaping.Escape(value);
            SimulateIncoming($"{CmdEcho},{wireValue};");

            Assert.NotNull(received);
            // ReadStringArg returns the raw (still-escaped) field; unescape to compare
            var raw = received.ReadStringArg();
            Assert.Equal(value, Escaping.Unescape(raw));
        }

        // ------------------------------------------------------------------ Multiple types in one command

        [Fact]
        public void MultipleTypes_SameCommand_AllReadCorrectly()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            // Cmd 1, int=42, bool=1 (true), float=3.14
            SimulateIncoming($"{CmdEcho},42,1,3.14;");

            Assert.NotNull(received);
            Assert.Equal(42, received.ReadInt32Arg());
            Assert.True(received.ReadBoolArg());
            Assert.Equal(3.14f, received.ReadFloatArg(), 2);
        }
    }
}
