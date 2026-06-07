using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Tests for the acknowledge (ACK) mechanism in CmdMessenger.
    ///
    /// When SendCommand is called with a non-zero ackCmdId and timeout, the library
    /// suspends the receive queue, sends the command, and blocks until a command with
    /// the matching ID arrives (or the timeout expires).
    ///
    /// Coverage:
    ///   - SendCommand with ackCmdId returns the ACK command when it arrives in time
    ///   - ACK received after other (unrelated) commands in the buffer still resolves correctly
    ///   - Host can ACK an embedded-initiated command (firmware sends, host ACKs via SendCommand)
    ///   - Timeout: when no ACK arrives the returned command is not Ok
    /// </summary>
    public class AcknowledgeTests : IDisposable
    {
        // Command IDs
        private const int CmdRequest   = 10;  // Host sends this
        private const int CmdAck       = 11;  // Firmware ACKs with this
        private const int CmdUnrelated = 99;  // Noise command
        private const int CmdFromBoard = 20;  // Embedded-initiated command

        private readonly LoopbackTransport _transport;
        private readonly CmdMessenger _messenger;

        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        public AcknowledgeTests()
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
        /// Injects bytes into the receive pipeline as if they arrived from the embedded side.
        /// </summary>
        private void SimulateIncoming(string commandString)
        {
            var bytes = Latin1.GetBytes(commandString);
            _transport.SimulateReceive(bytes);
        }

        /// <summary>
        /// Sends a command synchronously on a Task thread, returning the ReceivedCommand result.
        /// This keeps the test thread free so it can inject the ACK without deadlock.
        /// </summary>
        private Task<ReceivedCommand> SendAsync(SendCommand send)
        {
            return Task.Run(() =>
                _messenger.SendCommand(send, SendQueue.Default, ReceiveQueue.Default, UseQueue.BypassQueue));
        }

        // ------------------------------------------------------------------ basic ACK

        [Fact]
        public async Task SendCommand_WithAckCmdId_ReturnsAckCommand()
        {
            // Launch the blocking SendCommand on a background task
            var send = new SendCommand(CmdRequest, CmdAck, 1000);
            var sendTask = SendAsync(send);

            // Give the send a moment to start blocking, then inject the ACK
            await Task.Delay(50);
            SimulateIncoming($"{CmdAck};");

            var ack = await sendTask;

            Assert.NotNull(ack);
            Assert.True(ack.Ok);
            Assert.Equal(CmdAck, ack.CmdId);
        }

        // ------------------------------------------------------------------ ACK after noise

        [Fact]
        public async Task SendCommand_AckArrivesAfterUnrelatedCommands_StillResolves()
        {
            var send = new SendCommand(CmdRequest, CmdAck, 1000);
            var sendTask = SendAsync(send);

            await Task.Delay(30);
            // Two unrelated commands followed by the real ACK
            SimulateIncoming($"{CmdUnrelated},data;{CmdUnrelated},more;{CmdAck},payload;");

            var ack = await sendTask;

            Assert.NotNull(ack);
            Assert.True(ack.Ok);
            Assert.Equal(CmdAck, ack.CmdId);
        }

        // ------------------------------------------------------------------ ACK carries payload

        [Fact]
        public async Task SendCommand_AckWithArgument_ArgumentIsReadable()
        {
            const string expectedPayload = "ok";

            var send = new SendCommand(CmdRequest, CmdAck, 1000);
            var sendTask = SendAsync(send);

            await Task.Delay(50);
            SimulateIncoming($"{CmdAck},{expectedPayload};");

            var ack = await sendTask;

            Assert.NotNull(ack);
            Assert.True(ack.Ok);
            Assert.Equal(expectedPayload, ack.ReadStringArg());
        }

        // ------------------------------------------------------------------ timeout

        [Fact]
        public void SendCommand_NoAckArrives_ReturnsNotOkCommand()
        {
            // Deliberately do NOT inject an ACK — let it time out after 150 ms.
            // This test is intentionally synchronous: there is no background injection,
            // so no Task.Wait() deadlock risk.
            var send = new SendCommand(CmdRequest, CmdAck, 150);
            var ack = _messenger.SendCommand(send, SendQueue.Default, ReceiveQueue.Default, UseQueue.BypassQueue);

            Assert.NotNull(ack);
            Assert.False(ack.Ok);
        }

        // ------------------------------------------------------------------ host ACKs embedded command

        [Fact]
        public void EmbeddedSendsCommand_HostAcks_CallbackFires()
        {
            // Attach a callback: when the board sends CmdFromBoard the host
            // immediately sends back a non-ACK response command.
            ReceivedCommand boardCmd = null;
            _messenger.Attach(CmdFromBoard, cmd =>
            {
                boardCmd = cmd;
                // Host replies with CmdAck (no wait for further ACK from board)
                var ackBack = new SendCommand(CmdAck);
                _messenger.SendCommand(ackBack, SendQueue.Default, ReceiveQueue.Default, UseQueue.BypassQueue);
            });

            // Simulate the board initiating a command
            SimulateIncoming($"{CmdFromBoard},sensor_value;");
            Thread.Sleep(100);

            Assert.NotNull(boardCmd);
            Assert.Equal(CmdFromBoard, boardCmd.CmdId);
            Assert.Equal("sensor_value", boardCmd.ReadStringArg());
        }

        // ------------------------------------------------------------------ burst then ACK

        [Fact]
        public async Task AckArrivesAfterBurstOfCommands_StillResolves()
        {
            // Build a burst of 5 unrelated commands followed by the ACK
            var builder = new StringBuilder();
            for (int i = 0; i < 5; i++)
                builder.Append($"{CmdUnrelated},{i};");
            builder.Append($"{CmdAck};");
            var burst = builder.ToString();

            var send = new SendCommand(CmdRequest, CmdAck, 1000);
            var sendTask = SendAsync(send);

            await Task.Delay(30);
            SimulateIncoming(burst);

            var ack = await sendTask;

            Assert.NotNull(ack);
            Assert.True(ack.Ok);
            Assert.Equal(CmdAck, ack.CmdId);
        }
    }
}
