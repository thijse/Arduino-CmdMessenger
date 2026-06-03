"""Binary host-to-embedded value ping/pong scenarios.

Ports the logical coverage from legacy C# ``BinaryTextData.cs`` without requiring
hardware: binary bool/int/float/double, escaped strings, and special protocol
bytes embedded in binary float payloads.
"""
from __future__ import annotations

import itertools
import math
import random
import struct

import pytest

from cmd_messenger import CmdMessenger, binary_converter
from cmd_messenger.send_command import SendCommand
from cmd_messenger.enums import BoardType

from .firmware_simulator import DataType, LegacyCommand, LegacyValueFirmware, SimulatedFirmwareTransport


RANDOM = random.Random(23456)


class TestBinaryData:
    def setup_method(self):
        self.transport = SimulatedFirmwareTransport(LegacyValueFirmware().handle)
        self.messenger = CmdMessenger(self.transport, board_type=BoardType.BIT_16)
        self.transport.connect()

    def teardown_method(self):
        self.messenger.dispose()
        self.transport.dispose()

    @pytest.mark.parametrize("value", [False, True] + [RANDOM.choice([False, True]) for _ in range(100)])
    def test_binary_bool_round_trips(self, value):
        reply = self._value_ping_binary(DataType.BBOOL, binary_converter.byte_to_string(1 if value else 0))

        assert reply.ok
        assert reply.read_bin_bool_arg() is value

    @pytest.mark.parametrize("value", [-32768, -1234, 0, 1234, 32767] + [RANDOM.randint(-32768, 32767) for _ in range(100)])
    def test_binary_int16_round_trips(self, value):
        reply = self._value_ping_binary(DataType.BINT16, binary_converter.int16_to_string(value))

        assert reply.ok
        assert reply.read_bin_int16_arg() == value

    @pytest.mark.parametrize(
        "value",
        [-2147483648, -1, 0, 42, 2147483647] + [RANDOM.randint(-(2**31), 2**31 - 1) for _ in range(100)],
    )
    def test_binary_int32_round_trips(self, value):
        reply = self._value_ping_binary(DataType.BINT32, binary_converter.int32_to_string(value))

        assert reply.ok
        assert reply.read_bin_int32_arg() == value

    @pytest.mark.parametrize(
        "value",
        [0.0, 1.0, 15.0, 65535.0, 0.00390625, 2.3283064365386963e-10]
        + [RANDOM.uniform(-3.4e20, 3.4e20) for _ in range(200)],
    )
    def test_binary_float_round_trips(self, value):
        expected = _float32(value)
        reply = self._value_ping_binary(DataType.BFLOAT, binary_converter.float_to_string(value))

        assert reply.ok
        assert reply.read_bin_float_arg() == expected

    def test_binary_float_payloads_with_protocol_special_bytes_round_trip(self):
        special_bytes = [ord(";"), ord(","), ord("/"), 0, ord("a")]

        for raw_bytes in itertools.product(special_bytes, repeat=4):
            value = struct.unpack("<f", bytes(raw_bytes))[0]
            reply = self._value_ping_binary(DataType.BFLOAT, binary_converter.float_to_string(value))
            actual = reply.read_bin_float_arg()

            assert reply.ok
            if math.isnan(value):
                assert math.isnan(actual)
            else:
                assert actual == value

    @pytest.mark.parametrize(
        "value",
        [0.0, 1.0, -1.5, 3.14, -2.7844819605867e38]
        + [RANDOM.uniform(-3.4e20, 3.4e20) for _ in range(100)],
    )
    def test_binary_double_round_trips_as_bit16_float(self, value):
        expected = _float32(value)
        reply = self._value_ping_binary(DataType.BDOUBLE, binary_converter.float_to_string(value))
        actual = reply.read_bin_double_arg()

        assert reply.ok
        assert _close_float32(actual, expected)

    @pytest.mark.parametrize(
        "value",
        [
            "abcdefghijklmnopqrstuvwxyz",
            "abcde,fghijklmnopqrs,tuvwxyz",
            "abcde,fghijklmnopqrs,tuvwxyz,",
            "abc,defghij/klmnop//qr;stuvwxyz/",
            "abc,defghij/klmnop//qr;stuvwxyz//",
        ],
    )
    def test_escaped_string_round_trips(self, value):
        reply = self._value_ping_binary(DataType.ESC_STRING, binary_converter._bytes_to_escaped_string(value.encode("iso-8859-1")))

        assert reply.ok
        assert reply.read_bin_string_arg() == value

    def _value_ping_binary(self, data_type: DataType, encoded_value: str | None):
        assert encoded_value is not None
        command = SendCommand(
            LegacyCommand.VALUE_PING,
            ack_cmd_id=LegacyCommand.VALUE_PONG,
            timeout=1000,
        )
        command.add_argument(data_type.value)
        command.add_argument(encoded_value)
        return self.messenger.send_command(command)


def _float32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def _close_float32(actual: float, expected: float) -> bool:
    if actual == expected:
        return True
    tolerance = max(1e-6, abs(expected) * 1e-6)
    return abs(actual - expected) <= tolerance
