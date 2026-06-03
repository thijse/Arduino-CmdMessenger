"""Native firmware subprocess integration tests.

These mirror the C# ``LoopbackIntegrationTests`` layer: Python host stack talking
to the C++ loopback firmware over stdin/stdout pipes, with no real hardware.
"""
from __future__ import annotations

import threading

import pytest

from cmd_messenger import CmdMessenger
from cmd_messenger.enums import BoardType

from .firmware_process_transport import FirmwareProcessTransport
from .firmware_simulator import LoopbackCommand
from . import loopback_scenarios as scenarios


pytestmark = pytest.mark.integration


@pytest.fixture
def native_messenger(firmware_exe):
    transport = FirmwareProcessTransport(firmware_exe)
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    ready = threading.Event()
    messenger.attach(LoopbackCommand.ACKNOWLEDGE, lambda _cmd: ready.set())

    assert transport.connect()
    assert ready.wait(scenarios.BOOT_TIMEOUT_MS / 1000), "Firmware did not send boot ack"

    try:
        yield messenger
    finally:
        messenger.dispose()
        transport.dispose()


def test_ping_returns_pong(native_messenger):
    scenarios.assert_ping_returns_pong(native_messenger)


def test_echo_string_round_trips(native_messenger):
    scenarios.assert_echo_string_round_trips(native_messenger)


def test_echo_string_with_special_chars_round_trips(native_messenger):
    scenarios.assert_echo_string_with_special_chars_round_trips(native_messenger)


def test_add_floats_returns_sum_and_difference(native_messenger):
    scenarios.assert_add_floats_returns_sum_and_difference(native_messenger)


def test_echo_int32_round_trips(native_messenger):
    scenarios.assert_echo_int32_round_trips(native_messenger)


def test_echo_int16_round_trips(native_messenger):
    scenarios.assert_echo_int16_round_trips(native_messenger)


def test_echo_bool_round_trips(native_messenger):
    scenarios.assert_echo_bool_round_trips(native_messenger)


def test_echo_double_round_trips_as_float_text(native_messenger):
    scenarios.assert_echo_double_round_trips_as_float_text(native_messenger)


def test_multi_args_all_types_round_trip(native_messenger):
    scenarios.assert_multi_args_all_types_round_trip(native_messenger)


def test_unknown_command_triggers_error(native_messenger):
    scenarios.assert_unknown_command_triggers_error(native_messenger)


def test_repeated_commands_all_round_trip(native_messenger):
    scenarios.assert_repeated_commands_all_round_trip(native_messenger)
