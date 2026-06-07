using System;
using System.Text;
using System.Threading;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Integration tests for binary-mode argument round-trips through the full
    /// CmdMessenger pipeline using LoopbackTransport.
    ///
    /// Binary mode uses BinaryConverter to encode values as ISO-8859-1 escaped strings.
    /// The encoding guarantees that protocol special characters (','  ';'  '/'  '\0')
    /// embedded in the raw byte stream are escaped, so the parser can always recover the
    /// original bytes.
    ///
    /// Pattern:
    ///   1. Build a SendCommand with AddBinArgument(value).
    ///   2. Call InitArguments() to materialise the lazy argument list.
    ///   3. Build the wire string manually (CmdId + ',' + escapedArg + ';').
    ///   4. Inject it via SimulateReceive() so the full parse pipeline runs.
    ///   5. Read back via the matching ReadBin*Arg() method and assert equality.
    ///
    /// Coverage:
    ///   - bool via AddBinArgument(bool)   → ReadBinBoolArg()
    ///   - Int16 via AddBinArgument(Int16) → ReadBinInt16Arg()
    ///   - Int32 via AddBinArgument(Int32) → ReadBinInt32Arg()
    ///   - float via AddBinArgument(float) → ReadBinFloatArg()
    ///   - double via AddBinArgument(double) → ReadBinDoubleArg() (Bit16: encoded as float32)
    ///   - Payloads whose bytes collide with protocol special chars still round-trip correctly
    /// </summary>
    public class BinaryDataTests : IDisposable
    {
        private const int CmdEcho = 2;

        private readonly LoopbackTransport _transport;
        private readonly CmdMessenger _messenger;

        public BinaryDataTests()
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
        /// Injects a raw command string and waits for the queue to process it.
        /// </summary>
        private void SimulateIncoming(string commandString)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(commandString);
            _transport.SimulateReceive(bytes);
            Thread.Sleep(50);
        }

        /// <summary>
        /// Serialises a SendCommand to wire format without going through the full transport
        /// write path (which would loop the bytes back a second time).
        /// Returns the CmdId,escapedArg; string.
        /// </summary>
        private string ToWireString(SendCommand cmd)
        {
            cmd.InitArguments();
            var sb = new StringBuilder();
            sb.Append(cmd.CmdId);
            foreach (var arg in cmd.Arguments)
            {
                sb.Append(',').Append(arg);
            }
            sb.Append(';');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ bool

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BinBool_RoundTrip(bool value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinBoolArg());
        }

        // ------------------------------------------------------------------ Int16

        [Theory]
        [InlineData(short.MinValue)]
        [InlineData((short)-1)]
        [InlineData((short)0)]
        [InlineData((short)1)]
        [InlineData(short.MaxValue)]
        public void BinInt16_RoundTrip(short value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinInt16Arg());
        }

        // ------------------------------------------------------------------ Int32

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(int.MaxValue)]
        public void BinInt32_RoundTrip(int value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinInt32Arg());
        }

        // ------------------------------------------------------------------ float

        [Theory]
        [InlineData(0f)]
        [InlineData(1f)]
        [InlineData(-1f)]
        [InlineData(3.14159265f)]
        [InlineData(float.MaxValue)]
        [InlineData(float.MinValue)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void BinFloat_RoundTrip(float value)
        {
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            var result = received.ReadBinFloatArg();
            if (float.IsNaN(value))
                Assert.True(float.IsNaN(result));
            else
                Assert.Equal(value, result);
        }

        // ------------------------------------------------------------------ double (Bit16: stored as float32)

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(3.14159265)]
        public void BinDouble_Bit16_RoundTrip(double value)
        {
            // On Bit16 the library encodes double as float32 (via BinaryConverter.ToString(float)).
            // AddBinArgument(double) requires CommunicationManager.BoardType at serialization
            // time, which is only available when going through ExecuteSendCommand.
            // We therefore build the wire encoding manually: cast to float first, encode,
            // then inject — matching exactly what the real send path does on Bit16.
            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var asFloat = (float)value;
            var encoded = BinaryConverter.ToString(asFloat);
            SimulateIncoming($"{CmdEcho},{encoded};");

            Assert.NotNull(received);
            double result = received.ReadBinDoubleArg();
            // Compare at float precision
            Assert.Equal((double)asFloat, result, 5);
        }

        // ------------------------------------------------------------------ Special-byte collision tests

        [Fact]
        public void BinInt32_CommaBytes_RoundTrips()
        {
            // Build an Int32 value whose raw bytes are all ','  (0x2C)
            byte[] raw = { (byte)',', (byte)',', (byte)',', (byte)',' };
            int value = BitConverter.ToInt32(raw, 0);

            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinInt32Arg());
        }

        [Fact]
        public void BinInt32_SemicolonBytes_RoundTrips()
        {
            byte[] raw = { (byte)';', (byte)';', (byte)';', (byte)';' };
            int value = BitConverter.ToInt32(raw, 0);

            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinInt32Arg());
        }

        [Fact]
        public void BinInt32_SlashBytes_RoundTrips()
        {
            byte[] raw = { (byte)'/', (byte)'/', (byte)'/', (byte)'/' };
            int value = BitConverter.ToInt32(raw, 0);

            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinInt32Arg());
        }

        [Fact]
        public void BinInt32_NullBytes_RoundTrips()
        {
            // All-zero bytes
            int value = 0;

            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinInt32Arg());
        }

        [Fact]
        public void BinFloat_CommaBytesPayload_RoundTrips()
        {
            byte[] raw = { (byte)',', (byte)',', (byte)',', (byte)',' };
            float value = BitConverter.ToSingle(raw, 0);

            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinFloatArg());
        }

        [Fact]
        public void BinFloat_SlashBytesPayload_RoundTrips()
        {
            byte[] raw = { (byte)'/', (byte)'/', (byte)'/', (byte)'/' };
            float value = BitConverter.ToSingle(raw, 0);

            ReceivedCommand received = null;
            _messenger.Attach(CmdEcho, cmd => received = cmd);

            var send = new SendCommand(CmdEcho);
            send.AddBinArgument(value);
            SimulateIncoming(ToWireString(send));

            Assert.NotNull(received);
            Assert.Equal(value, received.ReadBinFloatArg());
        }
    }
}
