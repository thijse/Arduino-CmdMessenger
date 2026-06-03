"""Multiple binary argument ping/pong scenarios from legacy C# tests."""
from __future__ import annotations

import random
import struct

import pytest

from cmd_messenger import CmdMessenger, binary_converter
from cmd_messenger.send_command import SendCommand
from cmd_messenger.enums import BoardType

from .firmware_simulator import LegacyCommand, LegacyValueFirmware, SimulatedFirmwareTransport


RANDOM = random.Random(34567)


class TestMultipleArguments:
    def setup_method(self):
        self.transport = SimulatedFirmwareTransport(LegacyValueFirmware().handle)
        self.messenger = CmdMessenger(self.transport, board_type=BoardType.BIT_16)
        self.transport.connect()

    def teardown_method(self):
        self.messenger.dispose()
        self.transport.dispose()

    @pytest.mark.parametrize(
        ("int16_value", "int32_value", "double_value"),
        [(-11776, -1279916419, -2.7844819605867e38)]
        + [
            (
                RANDOM.randint(-32768, 32767),
                RANDOM.randint(-(2**31), 2**31 - 1),
                RANDOM.uniform(-3.4e20, 3.4e20),
            )
            for _ in range(250)
        ],
    )
    def test_binary_int16_int32_double_round_trip_in_one_command(self, int16_value, int32_value, double_value):
        command = SendCommand(
            LegacyCommand.MULTI_VALUE_PING,
            ack_cmd_id=LegacyCommand.MULTI_VALUE_PONG,
            timeout=1000,
        )
        command.add_argument(binary_converter.int16_to_string(int16_value))
        command.add_argument(binary_converter.int32_to_string(int32_value))
        command.add_argument(binary_converter.float_to_string(double_value))

        reply = self.messenger.send_command(command)

        assert reply.ok
        assert reply.read_bin_int16_arg() == int16_value
        assert reply.read_bin_int32_arg() == int32_value
        assert _close_float32(reply.read_bin_double_arg(), _float32(double_value))


def _float32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def _close_float32(actual: float, expected: float) -> bool:
    if actual == expected:
        return True
    tolerance = max(1e-6, abs(expected) * 1e-6)
    return abs(actual - expected) <= tolerance
