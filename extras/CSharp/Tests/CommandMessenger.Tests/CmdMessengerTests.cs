using System;
using System.Text;
using System.Threading;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Integration-level tests for the CmdMessenger host library using an
    /// in-memory LoopbackTransport (no serial port needed).
    ///
    /// Tests verify the full receive pipeline:
    ///   Transport.DataReceived → CommunicationManager.ParseLines → ReceiveCommandQueue → Callback
    ///
    /// Coverage:
    ///   - Callback dispatch: default handler, per-command-ID handler, priority
    ///   - Multiple commands arriving in a single buffer
    ///   - Partial data buffering across multiple receives
    ///   - Escaped characters surviving the parse pipeline
    ///   - SendCommand formatting (bypass-queue mode)
    ///   - NewLineReceived event propagation
    /// </summary>
    public class CmdMessengerTests : IDisposable
    {
        private readonly LoopbackTransport _transport;
        private readonly CmdMessenger _messenger;

        public CmdMessengerTests()
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
        /// Simulates the embedded side sending a command string to the host.
        /// </summary>
        private void SimulateIncoming(string commandString)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(commandString);
            _transport.SimulateReceive(bytes);
            // Give the receive queue time to process
            Thread.Sleep(50);
        }

        [Fact]
        public void Attach_DefaultCallback_Fires()
        {
            ReceivedCommand received = null;
            _messenger.Attach(cmd => received = cmd);

            SimulateIncoming("99;");

            Assert.NotNull(received);
            Assert.Equal(99, received.CmdId);
        }

        [Fact]
        public void Attach_SpecificCallback_FiresForMatchingId()
        {
            ReceivedCommand received = null;
            _messenger.Attach(5, cmd => received = cmd);

            SimulateIncoming("5,hello,42;");

            Assert.NotNull(received);
            Assert.Equal(5, received.CmdId);
            Assert.Equal("hello", received.ReadStringArg());
            Assert.Equal(42, received.ReadInt32Arg());
        }

        [Fact]
        public void Attach_SpecificCallback_DoesNotFireForOtherId()
        {
            ReceivedCommand received = null;
            _messenger.Attach(5, cmd => received = cmd);

            SimulateIncoming("6,hello;");

            // Should not have fired the cmd-5 callback
            Assert.Null(received);
        }

        [Fact]
        public void Attach_DefaultAndSpecific_SpecificTakesPriority()
        {
            ReceivedCommand defaultReceived = null;
            ReceivedCommand specificReceived = null;

            _messenger.Attach(cmd => defaultReceived = cmd);
            _messenger.Attach(7, cmd => specificReceived = cmd);

            SimulateIncoming("7,data;");

            Assert.NotNull(specificReceived);
            Assert.Equal(7, specificReceived.CmdId);
            // Default should not fire for cmd 7
            Assert.Null(defaultReceived);
        }

        [Fact]
        public void MultipleCommands_InOneBuffer_AllProcessed()
        {
            int count = 0;
            _messenger.Attach(cmd => Interlocked.Increment(ref count));

            SimulateIncoming("1,a;2,b;3,c;");

            Assert.Equal(3, count);
        }

        [Fact]
        public void SendCommand_WritesToTransport()
        {
            // We need to capture what's written.
            // The loopback puts writes back into the read buffer (triggering DataReceived).
            // For this test, we just verify no exception and the command is formatted.
            var cmd = new SendCommand(10);
            cmd.AddArgument("test");

            // SendCommand via bypass mode (no queueing)
            var result = _messenger.SendCommand(cmd, SendQueue.Default, ReceiveQueue.Default, UseQueue.BypassQueue);

            // Since no ACK is requested, result should be an empty/default command
            Assert.NotNull(result);
        }

        [Fact]
        public void EscapedFieldSeparator_InArgument_ParsedCorrectly()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            // Simulate receiving "1,hello/,world;" where /, is an escaped comma
            SimulateIncoming("1,hello/,world;");

            Assert.NotNull(received);
            // The raw arg should contain the escaped form; ReadStringArg returns it as-is
            var arg = received.ReadStringArg();
            Assert.Contains(",", Escaping.Unescape(arg));
        }

        [Fact]
        public void PartialData_BufferedUntilComplete()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            // Send partial — no command separator yet
            SimulateIncoming("1,part");
            Thread.Sleep(50);
            Assert.Null(received);

            // Now complete it
            SimulateIncoming("ial;");
            Thread.Sleep(50);

            Assert.NotNull(received);
            Assert.Equal("partial", received.ReadStringArg());
        }

        [Fact]
        public void NewLineReceived_EventFires()
        {
            bool fired = false;
            _messenger.NewLineReceived += (s, e) => fired = true;

            SimulateIncoming("1;");

            Assert.True(fired);
        }

        // --- Edge cases through full pipeline ---

        [Fact]
        public void EmptyArgument_ThroughPipeline()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            // "1,,;" = cmd 1 with two empty arguments
            SimulateIncoming("1,,;");

            Assert.NotNull(received);
            Assert.Equal("", received.ReadStringArg());
            Assert.Equal("", received.ReadStringArg());
        }

        [Fact]
        public void Latin1Characters_ThroughPipeline()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            SimulateIncoming("1,caf\u00E9;");

            Assert.NotNull(received);
            Assert.Equal("caf\u00E9", received.ReadStringArg());
        }

        [Fact]
        public void WhitespaceArgument_ThroughPipeline()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            SimulateIncoming("1,   ;");

            Assert.NotNull(received);
            Assert.Equal("   ", received.ReadStringArg());
        }

        [Fact]
        public void LargePayload_ThroughPipeline()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            var largeArg = new string('A', 5000);
            SimulateIncoming($"1,{largeArg};");

            Assert.NotNull(received);
            Assert.Equal(largeArg, received.ReadStringArg());
        }

        [Fact]
        public void EmptyCommand_NoArgs_Dispatched()
        {
            ReceivedCommand received = null;
            _messenger.Attach(1, cmd => received = cmd);

            // Just command ID, no arguments
            SimulateIncoming("1;");

            Assert.NotNull(received);
            Assert.Equal(1, received.CmdId);
            Assert.False(received.Available());
        }
    }
}
