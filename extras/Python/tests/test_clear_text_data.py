"""Clear-text host-to-embedded value ping/pong scenarios.

Ports the logical coverage from legacy C# ``ClearTextData.cs`` using an in-process
firmware responder. Real hardware host-to-embedded coverage is handled by
``test_hardware_loopback.py`` against the repo's LoopbackTestRunner sketch.
"""
from __future__ import annotations

import math
import random
import struct

import pytest

from cmd_messenger import CmdMessenger, SendCommand
from cmd_messenger.enums import BoardType

from .firmware_simulator import DataType, LegacyCommand, LegacyValueFirmware, SimulatedFirmwareTransport


RANDOM = random.Random(12345)


class TestClearTextData:
    def setup_method(self):
        self.transport = SimulatedFirmwareTransport(LegacyValueFirmware().handle)
        self.messenger = CmdMessenger(self.transport, board_type=BoardType.BIT_16)
        self.transport.connect()

    def teardown_method(self):
        self.messenger.dispose()
        self.transport.dispose()

    @pytest.mark.parametrize("value", [False, True] + [RANDOM.choice([False, True]) for _ in range(20)])
    def test_bool_round_trips(self, value):
        reply = self._value_ping(DataType.BOOL, value)

        assert reply.ok
        assert reply.read_bool_arg() is value

    @pytest.mark.parametrize("value", [-32768, -1234, 0, 1234, 32767] + [RANDOM.randint(-32768, 32767) for _ in range(100)])
    def test_int16_round_trips(self, value):
        reply = self._value_ping(DataType.INT16, value)

        assert reply.ok
        assert reply.read_int16_arg() == value

    @pytest.mark.parametrize(
        "value",
        [-2147483648, -1, 0, 42, 2147483647] + [RANDOM.randint(-(2**31), 2**31 - 1) for _ in range(100)],
    )
    def test_int32_round_trips(self, value):
        reply = self._value_ping(DataType.INT32, value)

        assert reply.ok
        assert reply.read_int32_arg() == value

    @pytest.mark.parametrize(
        "value",
        [0.0, 1.0, -1.5, 3.14, 65535.0]
        + [RANDOM.uniform(-1_000_000.0, 1_000_000.0) for _ in range(100)],
    )
    def test_float_round_trips(self, value):
        reply = self._value_ping(DataType.FLOAT, value)
        expected = _float32(value)

        assert reply.ok
        assert reply.read_float_arg() == expected

    @pytest.mark.parametrize(
        "value",
        [0.0, 1.0e-9, -1.0e-9, 3.4028235e20, -3.4028235e20]
        + [RANDOM.uniform(-3.4e20, 3.4e20) for _ in range(100)],
    )
    def test_float_scientific_round_trips_with_relative_tolerance(self, value):
        reply = self._value_ping(DataType.FLOAT_SCI, value)
        expected = _float32(value)
        actual = reply.read_float_arg()

        assert reply.ok
        assert _relative_error(expected, actual) <= 1e-6

    @pytest.mark.parametrize(
        "value",
        [0.0, 1.0e-9, -1.0e-9, 3.4028235e20, -3.4028235e20]
        + [RANDOM.uniform(-3.4e20, 3.4e20) for _ in range(100)],
    )
    def test_double_scientific_round_trips_as_bit16_float(self, value):
        reply = self._value_ping(DataType.DOUBLE_SCI, value)
        expected = _float32(value)
        actual = reply.read_double_arg()

        assert reply.ok
        assert _relative_error(expected, actual) <= 1e-6

    def _value_ping(self, data_type: DataType, value):
        command = SendCommand(
            LegacyCommand.VALUE_PING,
            ack_cmd_id=LegacyCommand.VALUE_PONG,
            timeout=1000,
        )
        command.add_argument(data_type.value)
        command.add_argument(value)
        return self.messenger.send_command(command)


def _float32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def _relative_error(expected: float, actual: float) -> float:
    if expected == actual:
        return 0.0
    if expected == 0:
        return abs(actual)
    return math.fabs((actual - expected) / expected)
