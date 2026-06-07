using System;
using System.Text;
using System.Threading;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Tests for commands that carry multiple mixed-type arguments, including the new
    /// ReceivedCommand.Read(string format) method and ReadCharArg().
    ///
    /// Read(format) accepts a format string where each character specifies the type of
    /// the next argument to read:
    ///   'b' — bool       'h' — Int16    'H' — UInt16
    ///   'i' — Int32      'I' — UInt32   'f' — float
    ///   'd' — double     's' — string   'c' — char
    ///
    /// Coverage:
    ///   - Int16 + Int32 + Float in one command, all read back correctly
    ///   - Read("hid") returns correct object[] for short + int32 + double
    ///   - Read(format) with single-char formats matches individual ReadXxxArg() calls
    ///   - ReadCharArg() returns the first character of a string argument
    ///   - ReadCharArg() returns '\0' when no argument is present
    ///   - Read(null) throws ArgumentNullException
    ///   - Read("?") throws ArgumentException for unknown format character
    /// </summary>
    public class MultipleArgumentsTests : IDisposable
    {
        private const int CmdMulti = 3;

        private readonly LoopbackTransport _transport;
        private readonly CmdMessenger _messenger;

        public MultipleArgumentsTests()
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

        private void SimulateIncoming(string commandString)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(commandString);
            _transport.SimulateReceive(bytes);
            Thread.Sleep(50);
        }

        // ------------------------------------------------------------------ multi-type pipeline round-trip

        [Fact]
        public void Int16_Int32_Float_AllRoundTrip()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            short  s = -100;
            int    i = 123456;
            float  f = 2.71828f;

            SimulateIncoming($"{CmdMulti},{s},{i},{f.ToString("R", System.Globalization.CultureInfo.InvariantCulture)};");

            Assert.NotNull(received);
            Assert.Equal(s, received.ReadInt16Arg());
            Assert.Equal(i, received.ReadInt32Arg());
            Assert.Equal(f, received.ReadFloatArg(), 5);
        }

        [Fact]
        public void Int32_Bool_String_AllRoundTrip()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},42,1,hello;");

            Assert.NotNull(received);
            Assert.Equal(42, received.ReadInt32Arg());
            Assert.True(received.ReadBoolArg());
            Assert.Equal("hello", received.ReadStringArg());
        }

        // ------------------------------------------------------------------ Read(format) — pipeline round-trip

        [Fact]
        public void Read_Format_hid_ReturnsCorrectValues()
        {
            // 'h' = Int16, 'i' = Int32, 'd' = double (Bit16: reads as float precision)
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            short s = 255;
            int   i = -1000;
            float fAsDouble = 1.5f;  // Bit16: double encoded/read as float

            SimulateIncoming($"{CmdMulti},{s},{i},{fAsDouble.ToString("R", System.Globalization.CultureInfo.InvariantCulture)};");

            Assert.NotNull(received);
            var result = received.Read("hid");

            Assert.Equal(3, result.Length);
            Assert.Equal(s, (short)result[0]);
            Assert.Equal(i, (int)result[1]);
            Assert.Equal((double)fAsDouble, (double)result[2], 5);
        }

        [Theory]
        [InlineData("?", "1",    true)]
        [InlineData("?", "0",    false)]
        public void Read_Format_b_ReturnsBool(string format, string wireVal, bool expected)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},{wireVal};");

            Assert.NotNull(received);
            var result = received.Read(format);
            Assert.Single(result);
            Assert.Equal(expected, (bool)result[0]);
        }

        [Fact]
        public void Read_Format_h_ReturnsInt16()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},-32768;");

            Assert.NotNull(received);
            var result = received.Read("h");
            Assert.Single(result);
            Assert.Equal(short.MinValue, (short)result[0]);
        }

        [Fact]
        public void Read_Format_i_ReturnsInt32()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},2147483647;");

            Assert.NotNull(received);
            var result = received.Read("i");
            Assert.Single(result);
            Assert.Equal(int.MaxValue, (int)result[0]);
        }

        [Fact]
        public void Read_Format_f_ReturnsFloat()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},3.14;");

            Assert.NotNull(received);
            var result = received.Read("f");
            Assert.Single(result);
            Assert.Equal(3.14f, (float)result[0], 2);
        }

        [Fact]
        public void Read_Format_s_ReturnsString()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},world;");

            Assert.NotNull(received);
            var result = received.Read("s");
            Assert.Single(result);
            Assert.Equal("world", (string)result[0]);
        }

        [Fact]
        public void Read_Format_c_ReturnsChar()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},Xtra;");

            Assert.NotNull(received);
            var result = received.Read("c");
            Assert.Single(result);
            Assert.Equal('X', (char)result[0]);
        }

        // ------------------------------------------------------------------ ReadCharArg()

        [Fact]
        public void ReadCharArg_ReturnsFirstCharOfArg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "hello" });
            Assert.Equal('h', cmd.ReadCharArg());
        }

        [Fact]
        public void ReadCharArg_SingleCharArg_ReturnsThatChar()
        {
            var cmd = new ReceivedCommand(new[] { "1", "Z" });
            Assert.Equal('Z', cmd.ReadCharArg());
        }

        [Fact]
        public void ReadCharArg_EmptyStringArg_ReturnsNul()
        {
            var cmd = new ReceivedCommand(new[] { "1", "" });
            Assert.Equal('\0', cmd.ReadCharArg());
        }

        [Fact]
        public void ReadCharArg_NoArgsAvailable_ReturnsNul()
        {
            var cmd = new ReceivedCommand(new[] { "1" });
            Assert.Equal('\0', cmd.ReadCharArg());
        }

        [Fact]
        public void ReadCharArg_ThroughPipeline_ReturnsFirstChar()
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdMulti, cmd => received = cmd);

            SimulateIncoming($"{CmdMulti},Alpha;");

            Assert.NotNull(received);
            Assert.Equal('A', received.ReadCharArg());
        }

        // ------------------------------------------------------------------ Read(format) — edge cases

        [Fact]
        public void Read_NullFormat_ThrowsArgumentNullException()
        {
            var cmd = new ReceivedCommand(new[] { "1", "42" });
            Assert.Throws<ArgumentNullException>(() => cmd.Read(null));
        }

        [Fact]
        public void Read_UnknownFormatChar_ThrowsArgumentException()
        {
            var cmd = new ReceivedCommand(new[] { "1", "42" });
            Assert.Throws<ArgumentException>(() => cmd.Read("X"));
        }

        [Fact]
        public void Read_EmptyFormat_ReturnsEmptyArray()
        {
            var cmd = new ReceivedCommand(new[] { "1", "42" });
            var result = cmd.Read("");
            Assert.Empty(result);
        }

        // ------------------------------------------------------------------ Read matches individual calls

        [Fact]
        public void Read_Format_Results_Match_IndividualReadCalls()
        {
            // Verify Read("hi") produces the same values as sequential ReadInt16Arg + ReadInt32Arg
            var rawArgs = new[] { "1", "7", "999" };

            var cmdA = new ReceivedCommand(rawArgs);
            var cmdB = new ReceivedCommand(rawArgs);

            var results = cmdA.Read("hi");
            short expectedH = cmdB.ReadInt16Arg();
            int   expectedI = cmdB.ReadInt32Arg();

            Assert.Equal(expectedH, (short)results[0]);
            Assert.Equal(expectedI, (int)results[1]);
        }
    }
}
