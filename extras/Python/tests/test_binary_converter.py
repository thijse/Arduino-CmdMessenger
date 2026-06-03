"""BinaryConverter parity tests.

Ports the important non-hardware coverage from the legacy C# binary data tests:
- little-endian round-trips for all primitive binary types
- protocol-special bytes inside binary payloads are escaped safely
- short inputs fail softly instead of raising
"""
from __future__ import annotations

import itertools
import math
import struct

import pytest

from cmd_messenger import binary_converter


@pytest.mark.parametrize(
    ("value", "encoder", "decoder"),
    [
        (-32768, binary_converter.int16_to_string, binary_converter.to_int16),
        (-1234, binary_converter.int16_to_string, binary_converter.to_int16),
        (32767, binary_converter.int16_to_string, binary_converter.to_int16),
        (0, binary_converter.uint16_to_string, binary_converter.to_uint16),
        (65535, binary_converter.uint16_to_string, binary_converter.to_uint16),
        (-2147483648, binary_converter.int32_to_string, binary_converter.to_int32),
        (-123456789, binary_converter.int32_to_string, binary_converter.to_int32),
        (2147483647, binary_converter.int32_to_string, binary_converter.to_int32),
        (0, binary_converter.uint32_to_string, binary_converter.to_uint32),
        (4294967295, binary_converter.uint32_to_string, binary_converter.to_uint32),
        (0, binary_converter.byte_to_string, binary_converter.to_byte),
        (255, binary_converter.byte_to_string, binary_converter.to_byte),
    ],
)
def test_integer_binary_round_trips(value, encoder, decoder):
    encoded = encoder(value)

    assert encoded is not None
    assert decoder(encoded) == value


@pytest.mark.parametrize("value", [0.0, 1.0, -1.5, 3.1415927, 65535.0, 2.3283064365386963e-10])
def test_float_binary_round_trips_as_float32(value):
    encoded = binary_converter.float_to_string(value)
    expected = struct.unpack("<f", struct.pack("<f", value))[0]

    assert encoded is not None
    assert binary_converter.to_float(encoded) == expected


@pytest.mark.parametrize("value", [0.0, 1.0, -1.5, math.pi, 1.5e100, -2.7844819605867e38])
def test_double_binary_round_trips(value):
    encoded = binary_converter.double_to_string(value)

    assert encoded is not None
    assert binary_converter.to_double(encoded) == value


def test_binary_float_payloads_escape_protocol_special_bytes():
    special_bytes = [ord(";"), ord(","), ord("/"), 0, ord("a")]

    for raw_bytes in itertools.product(special_bytes, repeat=4):
        value = struct.unpack("<f", bytes(raw_bytes))[0]
        encoded = binary_converter.float_to_string(value)

        assert encoded is not None
        assert binary_converter.to_float(encoded) == value


@pytest.mark.parametrize(
    ("encoded", "decoder"),
    [
        ("", binary_converter.to_byte),
        ("a", binary_converter.to_int16),
        ("abc", binary_converter.to_int32),
        ("abc", binary_converter.to_float),
        ("abcdefg", binary_converter.to_double),
    ],
)
def test_short_binary_inputs_return_none(encoded, decoder):
    assert decoder(encoded) is None


def test_escaped_string_to_bytes_round_trips_all_byte_values():
    payload = bytes(range(256))
    encoded = binary_converter._bytes_to_escaped_string(payload)

    assert encoded is not None
    assert binary_converter.escaped_string_to_bytes(encoded) == payload
