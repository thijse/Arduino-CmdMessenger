"""Acknowledgement behavior from the legacy C# Acknowledge tests."""
from __future__ import annotations

import threading
import time

from cmd_messenger import CmdMessenger, SendCommand
from cmd_messenger.enums import BoardType, SendQueue

from .firmware_simulator import LegacyCommand, LegacyValueFirmware, SimulatedFirmwareTransport


SETTLE_TIME = 0.10


class TestAcknowledgements:
    def setup_method(self):
        self.transport = SimulatedFirmwareTransport(LegacyValueFirmware().handle)
        self.messenger = CmdMessenger(self.transport, board_type=BoardType.BIT_16)
        self.transport.connect()

    def teardown_method(self):
        self.messenger.dispose()
        self.transport.dispose()

    def test_send_command_with_acknowledgement(self):
        reply = self.messenger.send_command(
            SendCommand(
                LegacyCommand.ARE_YOU_READY,
                ack_cmd_id=LegacyCommand.ACK,
                timeout=1000,
            )
        )

        assert reply.ok
        assert reply.cmd_id == LegacyCommand.ACK
        assert reply.read_string_arg() == "We are ready"

    def test_acknowledgement_after_queued_burst(self):
        for index in range(100):
            self.messenger.queue_command(SendCommand(90, index))

        reply = self.messenger.send_command(
            SendCommand(
                LegacyCommand.ARE_YOU_READY,
                ack_cmd_id=LegacyCommand.ACK,
                timeout=1000,
            ),
            send_queue_state=SendQueue.AT_END_QUEUE,
        )

        assert reply.ok
        assert reply.cmd_id == LegacyCommand.ACK
        written = b"".join(self.transport.writes).decode("iso-8859-1")
        assert written.count("90,") == 100
        assert f"{int(LegacyCommand.ARE_YOU_READY)};" in written

    def test_host_can_ack_embedded_initiated_command(self):
        got_ack = threading.Event()

        def on_are_you_ready(_cmd):
            self.messenger.send_command(SendCommand(LegacyCommand.ACK, "We are ready"))
            got_ack.set()

        self.messenger.attach(LegacyCommand.ARE_YOU_READY, on_are_you_ready)

        self.transport.simulate_receive(f"{int(LegacyCommand.ARE_YOU_READY)};".encode("iso-8859-1"))
        assert got_ack.wait(1.0)
        time.sleep(SETTLE_TIME)
        assert any(
            data.startswith(f"{int(LegacyCommand.ACK)},".encode("iso-8859-1"))
            for data in self.transport.writes
        )
