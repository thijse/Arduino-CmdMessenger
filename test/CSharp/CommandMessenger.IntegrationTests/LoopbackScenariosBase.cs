using System;
using System.Threading;
using CommandMessenger.Transport;
using Xunit;

namespace CommandMessenger.IntegrationTests
{
    /// <summary>
    /// Shared scenarios for cross-stack loopback testing. Subclasses provide a
    /// connected <see cref="ITransport"/> in <see cref="CreateTransport"/>; the
    /// same tests then run against the loopback (native subprocess) or real
    /// hardware (serial port).
    ///
    /// Command IDs MUST match both:
    ///   test/integration/firmware/src/main.cpp            (Layer 3a)
    ///   test/integration/sketch/src/LoopbackTestRunner.ino (Layer 3b)
    /// </summary>
    public abstract class LoopbackScenariosBase : IDisposable
    {
        protected const int kAcknowledge      = 0;
        protected const int kError            = 1;
        protected const int kEcho             = 2;
        protected const int kEchoResult       = 3;
        protected const int kAddFloats        = 4;
        protected const int kAddFloatsResult  = 5;
        protected const int kEchoInt          = 6;
        protected const int kEchoIntResult    = 7;
        protected const int kEchoBool         = 8;
        protected const int kEchoBoolResult   = 9;
        protected const int kMultiArgs        = 10;
        protected const int kMultiArgsResult  = 11;
        protected const int kPing             = 12;
        protected const int kPong             = 13;
        protected const int kEchoInt16        = 14;
        protected const int kEchoInt16Result  = 15;
        protected const int kEchoDouble       = 16;
        protected const int kEchoDoubleResult = 17;

        /// <summary>
        /// Per-test ACK timeout. Overridden by hardware fixture to allow for
        /// slower serial bring-up.
        /// </summary>
        protected virtual int AckTimeoutMs => 2000;

        /// <summary>
        /// Time to wait for the firmware boot acknowledgement on construction.
        /// </summary>
        protected virtual int BootTimeoutMs => 5000;

        protected readonly ITransport Transport;
        protected readonly CmdMessenger Messenger;

        protected LoopbackScenariosBase()
        {
            Transport = CreateTransport();
            Messenger = new CmdMessenger(Transport, BoardType.Bit16);
            Assert.True(Transport.Connect(), "Failed to connect transport");

            // Wait for the boot ack. Real Nano resets on serial open and needs ~1.5s
            // before sending it; the native subprocess sends it immediately.
            ReceivedCommand boot = null;
            using var ready = new ManualResetEventSlim();
            Messenger.Attach(kAcknowledge, cmd => { boot = cmd; ready.Set(); });
            Assert.True(ready.Wait(BootTimeoutMs),
                $"Firmware did not send boot ack within {BootTimeoutMs} ms");
            Assert.Equal(kAcknowledge, boot.CmdId);
        }

        protected abstract ITransport CreateTransport();

        public virtual void Dispose()
        {
            Messenger?.Dispose();
            Transport?.Dispose();
        }

        // --- Scenarios (run identically against any transport) ---

        [Fact]
        public void Ping_ReturnsPong()
        {
            var reply = Messenger.SendCommand(new SendCommand(kPing, kPong, AckTimeoutMs));
            Assert.True(reply.Ok, "Did not receive pong ack in time");
            Assert.Equal(kPong, reply.CmdId);
            Assert.Equal("pong", reply.ReadStringArg());
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("Hello, World!")]
        [InlineData("  spaced  ")]
        [InlineData("special chars: !@#$%^&*()")]
        public void Echo_String_RoundTrips(string text)
        {
            // C# SendCommand does not auto-escape text-mode string args; caller must Escape.
            // C# ReadStringArg also does not auto-unescape; caller must Unescape.
            // (C++ firmware: sendCmdEscArg escapes; readStringArg auto-unescapes.)
            var cmd = new SendCommand(kEcho, Escaping.Escape(text), kEchoResult, AckTimeoutMs);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok, $"No reply for echo '{text}'");
            Assert.Equal(text, Escaping.Unescape(reply.ReadStringArg()));
        }

        [Theory]
        [InlineData("contains, comma")]
        [InlineData("contains; semicolon")]
        [InlineData("contains/ slash")]
        [InlineData("a, b; c/ d")]
        public void Echo_StringWithSpecialChars_EscapedAndRoundTrips(string text)
        {
            var cmd = new SendCommand(kEcho, Escaping.Escape(text), kEchoResult, AckTimeoutMs);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok, $"No reply for escaped echo '{text}'");
            Assert.Equal(text, Escaping.Unescape(reply.ReadStringArg()));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(-5, 10)]
        [InlineData(3.14f, 2.71f)]
        [InlineData(1000.5f, -500.25f)]
        public void AddFloats_ReturnsSumAndDifference(float a, float b)
        {
            var cmd = new SendCommand(kAddFloats, kAddFloatsResult, AckTimeoutMs);
            cmd.AddArgument(a);
            cmd.AddArgument(b);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok, "No reply for add-floats");
            Assert.Equal(a + b, reply.ReadFloatArg(), 4);
            Assert.Equal(a - b, reply.ReadFloatArg(), 4);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(42)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void EchoInt32_RoundTrips(int value)
        {
            var cmd = new SendCommand(kEchoInt, value, kEchoIntResult, AckTimeoutMs);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok);
            Assert.Equal(value, reply.ReadInt32Arg());
        }

        [Theory]
        [InlineData((short)0)]
        [InlineData((short)1234)]
        [InlineData((short)-1234)]
        [InlineData(short.MaxValue)]
        [InlineData(short.MinValue)]
        public void EchoInt16_RoundTrips(short value)
        {
            var cmd = new SendCommand(kEchoInt16, value, kEchoInt16Result, AckTimeoutMs);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok);
            Assert.Equal(value, reply.ReadInt16Arg());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EchoBool_RoundTrips(bool value)
        {
            var cmd = new SendCommand(kEchoBool, kEchoBoolResult, AckTimeoutMs);
            cmd.AddArgument(value);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok);
            Assert.Equal(value ? 1 : 0, reply.ReadInt32Arg());
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(3.141592653589793)]
        [InlineData(-1.5e-10)]
        [InlineData(1.5e10)]
        public void EchoDouble_RoundTrips(double value)
        {
            var cmd = new SendCommand(kEchoDouble, kEchoDoubleResult, AckTimeoutMs);
            cmd.AddArgument(value);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok);
            // Firmware reads as double, sends as float (text mode = single precision).
            // Compare to single-precision rounding of the input.
            float expected = (float)value;
            float actual = reply.ReadFloatArg();
            float tol = Math.Max(1e-6f, Math.Abs(expected) * 1e-6f);
            Assert.InRange(actual, expected - tol, expected + tol);
        }

        [Fact]
        public void MultiArgs_AllTypesRoundTrip()
        {
            var cmd = new SendCommand(kMultiArgs, kMultiArgsResult, AckTimeoutMs);
            cmd.AddArgument((short)1234);
            cmd.AddArgument(3.14f);
            cmd.AddArgument(Escaping.Escape("mixed, args; here"));
            cmd.AddArgument(true);
            var reply = Messenger.SendCommand(cmd);
            Assert.True(reply.Ok, "No reply for multi-args");
            Assert.Equal((short)1234, reply.ReadInt16Arg());
            Assert.Equal(3.14f, reply.ReadFloatArg(), 4);
            Assert.Equal("mixed, args; here", Escaping.Unescape(reply.ReadStringArg()));
            Assert.Equal(1, reply.ReadInt32Arg());
        }

        [Fact]
        public void UnknownCommand_TriggersError()
        {
            ReceivedCommand err = null;
            using var got = new ManualResetEventSlim();
            Messenger.Attach(kError, cmd => { err = cmd; got.Set(); });

            // Use a command ID that has no handler
            Messenger.SendCommand(new SendCommand(99));

            Assert.True(got.Wait(AckTimeoutMs), "Did not receive error response");
            Assert.Equal(kError, err.CmdId);
        }

        [Fact]
        public void RepeatedCommands_AllRoundTrip()
        {
            for (int i = 0; i < 20; i++)
            {
                var cmd = new SendCommand(kEchoInt, i, kEchoIntResult, AckTimeoutMs);
                var reply = Messenger.SendCommand(cmd);
                Assert.True(reply.Ok, $"Iteration {i} failed to ack");
                Assert.Equal(i, reply.ReadInt32Arg());
            }
        }
    }
}
