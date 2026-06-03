"""Shared host-to-embedded loopback scenarios.

These mirror ``test/CSharp/CommandMessenger.IntegrationTests/LoopbackScenariosBase.cs``
and are intentionally transport-agnostic.
"""
from __future__ import annotations

import math
import struct
import threading

from cmd_messenger import CmdMessenger, SendCommand, escaping

from .firmware_simulator import LoopbackCommand


ACK_TIMEOUT_MS = 2000
BOOT_TIMEOUT_MS = 5000


def wait_for_boot_ack(messenger: CmdMessenger, timeout_ms: int = BOOT_TIMEOUT_MS) -> None:
    ready = threading.Event()
    boot_commands = []

    messenger.attach(LoopbackCommand.ACKNOWLEDGE, lambda cmd: (boot_commands.append(cmd), ready.set()))
    assert ready.wait(timeout_ms / 1000), "Firmware did not send boot ack"
    assert boot_commands[0].cmd_id == LoopbackCommand.ACKNOWLEDGE


def assert_ping_returns_pong(messenger: CmdMessenger) -> None:
    reply = messenger.send_command(
        SendCommand(LoopbackCommand.PING, ack_cmd_id=LoopbackCommand.PONG, timeout=ACK_TIMEOUT_MS)
    )

    assert reply.ok
    assert reply.cmd_id == LoopbackCommand.PONG
    assert reply.read_string_arg() == "pong"


def assert_echo_string_round_trips(messenger: CmdMessenger) -> None:
    for text in ["hello", "Hello, World!", "  spaced  ", "special chars: !@#$%^&*()"]:
        _assert_echo_string(messenger, text)


def assert_echo_string_with_special_chars_round_trips(messenger: CmdMessenger) -> None:
    for text in ["contains, comma", "contains; semicolon", "contains/ slash", "a, b; c/ d"]:
        _assert_echo_string(messenger, text)


def assert_add_floats_returns_sum_and_difference(messenger: CmdMessenger) -> None:
    for first, second in [(0.0, 0.0), (1.0, 2.0), (-5.0, 10.0), (3.14, 2.71), (1000.5, -500.25)]:
        command = SendCommand(
            LoopbackCommand.ADD_FLOATS,
            ack_cmd_id=LoopbackCommand.ADD_FLOATS_RESULT,
            timeout=ACK_TIMEOUT_MS,
        )
        command.add_argument(first)
        command.add_argument(second)
        reply = messenger.send_command(command)

        assert reply.ok
        assert math.isclose(reply.read_float_arg(), first + second, rel_tol=1e-5, abs_tol=1e-5)
        assert math.isclose(reply.read_float_arg(), first - second, rel_tol=1e-5, abs_tol=1e-5)


def assert_echo_int32_round_trips(messenger: CmdMessenger) -> None:
    for value in [0, 42, -1, 2**31 - 1, -(2**31)]:
        reply = messenger.send_command(
            SendCommand(
                LoopbackCommand.ECHO_INT,
                value,
                ack_cmd_id=LoopbackCommand.ECHO_INT_RESULT,
                timeout=ACK_TIMEOUT_MS,
            )
        )

        assert reply.ok
        assert reply.read_int32_arg() == value


def assert_echo_int16_round_trips(messenger: CmdMessenger) -> None:
    for value in [0, 1234, -1234, 32767, -32768]:
        reply = messenger.send_command(
            SendCommand(
                LoopbackCommand.ECHO_INT16,
                value,
                ack_cmd_id=LoopbackCommand.ECHO_INT16_RESULT,
                timeout=ACK_TIMEOUT_MS,
            )
        )

        assert reply.ok
        assert reply.read_int16_arg() == value


def assert_echo_bool_round_trips(messenger: CmdMessenger) -> None:
    for value in [True, False]:
        command = SendCommand(
            LoopbackCommand.ECHO_BOOL,
            ack_cmd_id=LoopbackCommand.ECHO_BOOL_RESULT,
            timeout=ACK_TIMEOUT_MS,
        )
        command.add_argument(value)
        reply = messenger.send_command(command)

        assert reply.ok
        assert reply.read_int32_arg() == (1 if value else 0)


def assert_echo_double_round_trips_as_float_text(messenger: CmdMessenger) -> None:
    for value in [0.0, 3.141592653589793, -1.5e-10, 1.5e10]:
        command = SendCommand(
            LoopbackCommand.ECHO_DOUBLE,
            ack_cmd_id=LoopbackCommand.ECHO_DOUBLE_RESULT,
            timeout=ACK_TIMEOUT_MS,
        )
        command.add_argument(value)
        reply = messenger.send_command(command)

        assert reply.ok
        expected = _float32_text_value(value)
        actual = reply.read_float_arg()
        tolerance = max(1e-6, abs(expected) * 1e-6)
        assert expected - tolerance <= actual <= expected + tolerance


def assert_multi_args_all_types_round_trip(messenger: CmdMessenger) -> None:
    command = SendCommand(
        LoopbackCommand.MULTI_ARGS,
        ack_cmd_id=LoopbackCommand.MULTI_ARGS_RESULT,
        timeout=ACK_TIMEOUT_MS,
    )
    command.add_argument(1234)
    command.add_argument(3.14)
    command.add_argument(escaping.escape("mixed, args; here"))
    command.add_argument(True)

    reply = messenger.send_command(command)

    assert reply.ok
    assert reply.read_int16_arg() == 1234
    assert math.isclose(reply.read_float_arg(), 3.14, rel_tol=1e-5, abs_tol=1e-5)
    assert escaping.unescape(reply.read_string_arg()) == "mixed, args; here"
    assert reply.read_int32_arg() == 1


def assert_unknown_command_triggers_error(messenger: CmdMessenger) -> None:
    received = []
    event = threading.Event()
    messenger.attach(LoopbackCommand.ERROR, lambda cmd: (received.append(cmd), event.set()))

    messenger.send_command(SendCommand(99))

    assert event.wait(ACK_TIMEOUT_MS / 1000)
    assert received[0].cmd_id == LoopbackCommand.ERROR


def assert_repeated_commands_all_round_trip(messenger: CmdMessenger) -> None:
    for index in range(20):
        reply = messenger.send_command(
            SendCommand(
                LoopbackCommand.ECHO_INT,
                index,
                ack_cmd_id=LoopbackCommand.ECHO_INT_RESULT,
                timeout=ACK_TIMEOUT_MS,
            )
        )

        assert reply.ok
        assert reply.read_int32_arg() == index


def _assert_echo_string(messenger: CmdMessenger, text: str) -> None:
    reply = messenger.send_command(
        SendCommand(
            LoopbackCommand.ECHO,
            escaping.escape(text),
            ack_cmd_id=LoopbackCommand.ECHO_RESULT,
            timeout=ACK_TIMEOUT_MS,
        )
    )

    assert reply.ok
    assert escaping.unescape(reply.read_string_arg()) == text


def _float32_text_value(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]
