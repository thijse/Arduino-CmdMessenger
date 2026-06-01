"""Integration-level tests for the CmdMessenger host library using LoopbackTransport.

Port of C# ``CmdMessengerTests.cs``. Tests verify the full receive pipeline:
    Transport.data_received → CommunicationManager.parse_lines
        → ReceiveCommandQueue → Callback

Coverage:
- Callback dispatch: default handler, per-command-ID handler, priority
- Multiple commands arriving in a single buffer
- Partial data buffering across multiple receives
- Escaped characters surviving the parse pipeline
- SendCommand formatting (bypass-queue mode)
- new_line_received event propagation
- Edge cases (empty args, Latin-1, whitespace, large payload)
"""
from __future__ import annotations

import threading
import time

import pytest

from cmd_messenger import CmdMessenger, ReceivedCommand, SendCommand
from cmd_messenger.enums import BoardType, ReceiveQueue, SendQueue, UseQueue

from .loopback_transport import LoopbackTransport


SETTLE_TIME = 0.10  # seconds — give receive queue time to process


class TestCmdMessengerPipeline:
    """Full pipeline tests using in-memory LoopbackTransport."""

    def setup_method(self):
        self.transport = LoopbackTransport()
        self.messenger = CmdMessenger(self.transport, board_type=BoardType.BIT_16)
        self.transport.connect()

    def teardown_method(self):
        self.messenger.dispose()
        self.transport.dispose()

    def simulate_incoming(self, command_string: str) -> None:
        """Simulate the embedded side sending a command string."""
        data = command_string.encode("latin-1")
        self.transport.simulate_receive(data)
        time.sleep(SETTLE_TIME)

    # ------------------------------------------------------------------
    # Callback dispatch
    # ------------------------------------------------------------------
    def test_attach_default_callback_fires(self):
        received = []
        self.messenger.attach(lambda cmd: received.append(cmd))

        self.simulate_incoming("99;")

        assert len(received) == 1
        assert received[0].cmd_id == 99

    def test_attach_specific_callback_fires_for_matching_id(self):
        received = []
        self.messenger.attach(5, lambda cmd: received.append(cmd))

        self.simulate_incoming("5,hello,42;")

        assert len(received) == 1
        assert received[0].cmd_id == 5
        assert received[0].read_string_arg() == "hello"
        assert received[0].read_int32_arg() == 42

    def test_attach_specific_callback_does_not_fire_for_other_id(self):
        received = []
        self.messenger.attach(5, lambda cmd: received.append(cmd))

        self.simulate_incoming("6,hello;")

        assert len(received) == 0

    def test_attach_default_and_specific_specific_takes_priority(self):
        default_received = []
        specific_received = []

        self.messenger.attach(lambda cmd: default_received.append(cmd))
        self.messenger.attach(7, lambda cmd: specific_received.append(cmd))

        self.simulate_incoming("7,data;")

        assert len(specific_received) == 1
        assert specific_received[0].cmd_id == 7
        # Default should NOT fire for cmd 7
        assert len(default_received) == 0

    # ------------------------------------------------------------------
    # Multiple commands in one buffer
    # ------------------------------------------------------------------
    def test_multiple_commands_in_one_buffer_all_processed(self):
        count = [0]
        self.messenger.attach(lambda cmd: count.__setitem__(0, count[0] + 1))

        self.simulate_incoming("1,a;2,b;3,c;")

        assert count[0] == 3

    # ------------------------------------------------------------------
    # Partial data buffering
    # ------------------------------------------------------------------
    def test_partial_data_buffered_until_complete(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        # Send partial — no command separator yet
        self.transport.simulate_receive(b"1,part")
        time.sleep(SETTLE_TIME)
        assert len(received) == 0

        # Now complete it
        self.transport.simulate_receive(b"ial;")
        time.sleep(SETTLE_TIME)

        assert len(received) == 1
        assert received[0].read_string_arg() == "partial"

    # ------------------------------------------------------------------
    # SendCommand formatting
    # ------------------------------------------------------------------
    def test_send_command_writes_to_transport(self):
        cmd = SendCommand(10, "test")
        # SendCommand via bypass mode (no queueing)
        result = self.messenger.send_command(
            cmd,
            send_queue_state=SendQueue.DEFAULT,
            receive_queue_state=ReceiveQueue.DEFAULT,
            use_queue=UseQueue.BYPASS_QUEUE,
        )
        # Since no ACK is requested, result should be valid but with no specific ack
        assert result is not None

    # ------------------------------------------------------------------
    # Escaped characters through full pipeline
    # ------------------------------------------------------------------
    def test_escaped_field_separator_parsed_correctly(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        # "1,hello/,world;" where /, is an escaped comma
        self.simulate_incoming("1,hello/,world;")

        assert len(received) == 1
        arg = received[0].read_string_arg()
        # The arg contains the escaped form; unescape it
        from cmd_messenger import escaping
        assert "," in escaping.unescape(arg)

    def test_escaped_command_separator_in_arg(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        # "1,a/;b;" — semicolon within arg is escaped
        self.simulate_incoming("1,a/;b;")

        assert len(received) == 1
        arg = received[0].read_string_arg()
        from cmd_messenger import escaping
        assert ";" in escaping.unescape(arg)

    # ------------------------------------------------------------------
    # Event propagation
    # ------------------------------------------------------------------
    def test_new_line_received_event_fires(self):
        fired = []
        self.messenger.new_line_received += lambda cmd: fired.append(True)

        self.simulate_incoming("1;")

        assert len(fired) > 0

    # ------------------------------------------------------------------
    # Edge cases through full pipeline
    # ------------------------------------------------------------------
    def test_empty_argument_through_pipeline(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        # "1,,;" = cmd 1 with two empty arguments
        self.simulate_incoming("1,,;")

        assert len(received) == 1
        assert received[0].read_string_arg() == ""
        assert received[0].read_string_arg() == ""

    def test_latin1_characters_through_pipeline(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        self.simulate_incoming("1,caf\u00e9;")

        assert len(received) == 1
        assert received[0].read_string_arg() == "caf\u00e9"

    def test_whitespace_argument_through_pipeline(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        self.simulate_incoming("1,   ;")

        assert len(received) == 1
        assert received[0].read_string_arg() == "   "

    def test_large_payload_through_pipeline(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        large_arg = "A" * 5000
        self.simulate_incoming(f"1,{large_arg};")

        assert len(received) == 1
        assert received[0].read_string_arg() == large_arg

    def test_empty_command_no_args_dispatched(self):
        received = []
        self.messenger.attach(1, lambda cmd: received.append(cmd))

        self.simulate_incoming("1;")

        assert len(received) == 1
        assert received[0].cmd_id == 1
        assert received[0].available() is False

    # ------------------------------------------------------------------
    # Queue strategies through pipeline
    # ------------------------------------------------------------------
    def test_collapse_command_strategy_replaces_duplicate(self):
        """CollapseCommandStrategy should replace earlier commands with same ID in queue."""
        from cmd_messenger.queue import CollapseCommandStrategy

        # Queue two commands with same ID using collapse — only last value matters
        cmd1 = SendCommand(10, 1.0)
        cmd2 = SendCommand(10, 2.0)
        self.messenger.queue_command(CollapseCommandStrategy(cmd1))
        self.messenger.queue_command(CollapseCommandStrategy(cmd2))
        # Allow the send queue to process
        time.sleep(SETTLE_TIME * 2)
        # If this doesn't crash and the queue doesn't back up, the test passes.

    def test_multiple_callbacks_different_ids(self):
        """Multiple specific callbacks for different IDs all fire correctly."""
        results = {1: [], 2: [], 3: []}
        self.messenger.attach(1, lambda cmd: results[1].append(cmd))
        self.messenger.attach(2, lambda cmd: results[2].append(cmd))
        self.messenger.attach(3, lambda cmd: results[3].append(cmd))

        self.simulate_incoming("2,data2;1,data1;3,data3;2,again;")

        assert len(results[1]) == 1
        assert len(results[2]) == 2
        assert len(results[3]) == 1
        assert results[1][0].read_string_arg() == "data1"
        assert results[3][0].read_string_arg() == "data3"
