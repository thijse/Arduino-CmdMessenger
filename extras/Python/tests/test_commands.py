"""Tests for SendCommand and ReceivedCommand data classes.

Port of C# ``CommandTests.cs``. Tests verify:
- SendCommand: CmdId, Ok flag, typed argument serialisation (string, float,
  int16/uint16/int32, bool), ACK properties.
- ReceivedCommand: parsing raw args, sequential ReadXxxArg() calls, boundary
  cases (empty, overflow, NaN/Infinity), format-string reader.
"""
from __future__ import annotations

import math

import pytest

from cmd_messenger import ReceivedCommand, SendCommand
from cmd_messenger.enums import BoardType


# =====================================================================
# SendCommand
# =====================================================================
class TestSendCommand:
    def test_cmd_id_is_set(self):
        cmd = SendCommand(5)
        assert cmd.cmd_id == 5
        assert cmd.ok is True

    def test_negative_id_not_ok(self):
        cmd = SendCommand(-1)
        assert cmd.ok is False

    def test_with_string_arg(self):
        cmd = SendCommand(3, "hello")
        cmd.init_arguments()
        assert cmd.arguments == ["hello"]

    def test_with_multiple_string_args(self):
        cmd = SendCommand(3, "a", "b", "c")
        cmd.init_arguments()
        assert cmd.arguments == ["a", "b", "c"]

    def test_with_float_arg(self):
        cmd = SendCommand(1, 3.14)
        cmd.init_arguments()
        # float repr should contain "3.14"
        assert "3.14" in cmd.arguments[0]

    def test_with_int_arg(self):
        cmd = SendCommand(1, 123456)
        cmd.init_arguments()
        assert cmd.arguments[0] == "123456"

    def test_with_negative_int(self):
        cmd = SendCommand(1, -42)
        cmd.init_arguments()
        assert cmd.arguments[0] == "-42"

    def test_with_bool_true(self):
        cmd = SendCommand(1, True)
        cmd.init_arguments()
        assert cmd.arguments[0] == "1"

    def test_with_bool_false(self):
        cmd = SendCommand(1, False)
        cmd.init_arguments()
        assert cmd.arguments[0] == "0"

    def test_ack_properties(self):
        cmd = SendCommand(5, ack_cmd_id=10, timeout=2000)
        assert cmd.req_ac is True
        assert cmd.ack_cmd_id == 10
        assert cmd.timeout == 2000

    def test_no_ack_properties(self):
        cmd = SendCommand(5)
        assert cmd.req_ac is False
        assert cmd.ack_cmd_id == 0
        assert cmd.timeout == 0

    def test_empty_string_arg(self):
        cmd = SendCommand(1, "")
        cmd.init_arguments()
        assert cmd.arguments == [""]

    def test_whitespace_arg(self):
        cmd = SendCommand(1, "  \t  ")
        cmd.init_arguments()
        assert cmd.arguments[0] == "  \t  "

    def test_latin1_string_arg(self):
        cmd = SendCommand(1, "café")
        cmd.init_arguments()
        assert cmd.arguments[0] == "café"

    def test_list_arg_expanded(self):
        cmd = SendCommand(3, ["a", "b", "c"])
        cmd.init_arguments()
        assert cmd.arguments == ["a", "b", "c"]


# =====================================================================
# ReceivedCommand
# =====================================================================
class TestReceivedCommand:
    def test_parses_raw_arguments(self):
        cmd = ReceivedCommand(["3", "hello", "42"])
        assert cmd.cmd_id == 3
        assert cmd.ok is True

    def test_read_string_arg(self):
        cmd = ReceivedCommand(["1", "world"])
        assert cmd.read_string_arg() == "world"

    def test_read_int16_arg(self):
        cmd = ReceivedCommand(["1", "-100"])
        assert cmd.read_int16_arg() == -100

    def test_read_uint16_arg(self):
        cmd = ReceivedCommand(["1", "65000"])
        assert cmd.read_uint16_arg() == 65000

    def test_read_int32_arg(self):
        cmd = ReceivedCommand(["1", "123456"])
        assert cmd.read_int32_arg() == 123456

    def test_read_uint32_arg(self):
        cmd = ReceivedCommand(["1", "4000000000"])
        assert cmd.read_uint32_arg() == 4000000000

    def test_read_float_arg(self):
        cmd = ReceivedCommand(["1", "3.14"])
        assert abs(cmd.read_float_arg() - 3.14) < 0.001

    def test_read_bool_arg_true(self):
        cmd = ReceivedCommand(["1", "1"])
        assert cmd.read_bool_arg() is True

    def test_read_bool_arg_false(self):
        cmd = ReceivedCommand(["1", "0"])
        assert cmd.read_bool_arg() is False

    def test_multiple_args_read_sequentially(self):
        cmd = ReceivedCommand(["2", "hello", "42", "3.14"])
        assert cmd.read_string_arg() == "hello"
        assert cmd.read_int32_arg() == 42
        assert abs(cmd.read_float_arg() - 3.14) < 0.001

    def test_read_beyond_available_returns_default(self):
        cmd = ReceivedCommand(["1", "42"])
        assert cmd.read_int32_arg() == 42
        # No more args — should return 0
        assert cmd.read_int32_arg() == 0

    def test_available_false_when_empty(self):
        cmd = ReceivedCommand(["1"])  # no args
        assert cmd.available() is False

    def test_available_true_when_has_args(self):
        cmd = ReceivedCommand(["1", "hello"])
        assert cmd.available() is True

    def test_none_raw_args_not_ok(self):
        cmd = ReceivedCommand(None)
        assert cmd.ok is False

    def test_empty_raw_args_not_ok(self):
        cmd = ReceivedCommand([])
        assert cmd.ok is False

    def test_invalid_cmd_id_not_ok(self):
        cmd = ReceivedCommand(["abc"])
        assert cmd.ok is False

    # --- Numeric boundaries ---
    @pytest.mark.parametrize("raw,expected", [
        ("2147483647", 2147483647),    # Int32 max
        ("-2147483648", -2147483648),  # Int32 min
        ("0", 0),
    ])
    def test_read_int32_arg_boundaries(self, raw, expected):
        cmd = ReceivedCommand(["1", raw])
        assert cmd.read_int32_arg() == expected

    def test_read_int32_arg_overflow_returns_zero(self):
        cmd = ReceivedCommand(["1", "9999999999999"])
        assert cmd.read_int32_arg() == 0

    def test_read_int32_arg_empty_string_returns_zero(self):
        cmd = ReceivedCommand(["1", ""])
        assert cmd.read_int32_arg() == 0

    @pytest.mark.parametrize("raw,expected", [
        ("65535", 65535),
        ("0", 0),
    ])
    def test_read_uint16_arg_boundaries(self, raw, expected):
        cmd = ReceivedCommand(["1", raw])
        assert cmd.read_uint16_arg() == expected

    def test_read_float_arg_nan(self):
        cmd = ReceivedCommand(["1", "nan"])
        assert math.isnan(cmd.read_float_arg())

    def test_read_float_arg_infinity(self):
        cmd = ReceivedCommand(["1", "inf"])
        result = cmd.read_float_arg()
        assert math.isinf(result) and result > 0

    def test_read_float_arg_negative_infinity(self):
        cmd = ReceivedCommand(["1", "-inf"])
        result = cmd.read_float_arg()
        assert math.isinf(result) and result < 0

    def test_read_float_arg_empty_returns_zero(self):
        cmd = ReceivedCommand(["1", ""])
        assert cmd.read_float_arg() == 0.0

    # --- String edge cases ---
    def test_read_string_arg_empty(self):
        cmd = ReceivedCommand(["1", ""])
        assert cmd.read_string_arg() == ""

    def test_read_string_arg_whitespace(self):
        cmd = ReceivedCommand(["1", "   "])
        assert cmd.read_string_arg() == "   "

    # --- Format-string reader ---
    def test_read_format_string_basic(self):
        cmd = ReceivedCommand(["2", "hello", "42", "3.14"])
        s, i, f = cmd.read("sif")
        assert s == "hello"
        assert i == 42
        assert abs(f - 3.14) < 0.001

    def test_read_format_bool(self):
        cmd = ReceivedCommand(["1", "1", "0"])
        t, f = cmd.read("??")
        assert t is True
        assert f is False
