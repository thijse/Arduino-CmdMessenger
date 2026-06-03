"""Hardware-in-the-loop host-to-embedded tests.

These tests require a board running ``test/integration/sketch/src/LoopbackTestRunner.ino``.
They are skipped unless pytest is run with ``--hardware`` and a serial port is
provided via ``CMDMESSENGER_PORT`` or ``CMDMSG_HW_PORT``.
"""
from __future__ import annotations

import threading

import pytest

from cmd_messenger import CmdMessenger, SendCommand
from cmd_messenger.enums import BoardType
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

from .firmware_simulator import LoopbackCommand
from . import loopback_scenarios as scenarios


pytestmark = pytest.mark.hardware


@pytest.fixture
def hardware_messenger(serial_port, serial_baud):
    settings = SerialSettings(port_name=serial_port, baud_rate=serial_baud, timeout=3000)
    transport = SerialTransport(settings)
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True

    ready = threading.Event()
    messenger.attach(LoopbackCommand.ACKNOWLEDGE, lambda _cmd: ready.set())

    assert messenger.connect()
    if not ready.wait(8.0):
        reply = messenger.send_command(
            SendCommand(LoopbackCommand.PING, ack_cmd_id=LoopbackCommand.PONG, timeout=3000)
        )
        assert reply.ok, "Board did not send boot ack and did not respond to ping"

    try:
        yield messenger
    finally:
        messenger.dispose()
        transport.dispose()


def test_ping_returns_pong(hardware_messenger):
    scenarios.assert_ping_returns_pong(hardware_messenger)


def test_echo_string_round_trips(hardware_messenger):
    scenarios.assert_echo_string_round_trips(hardware_messenger)


def test_echo_string_with_special_chars_round_trips(hardware_messenger):
    scenarios.assert_echo_string_with_special_chars_round_trips(hardware_messenger)


def test_add_floats_returns_sum_and_difference(hardware_messenger):
    scenarios.assert_add_floats_returns_sum_and_difference(hardware_messenger)


def test_echo_int32_round_trips(hardware_messenger):
    scenarios.assert_echo_int32_round_trips(hardware_messenger)


def test_echo_int16_round_trips(hardware_messenger):
    scenarios.assert_echo_int16_round_trips(hardware_messenger)


def test_echo_bool_round_trips(hardware_messenger):
    scenarios.assert_echo_bool_round_trips(hardware_messenger)


def test_echo_double_round_trips_as_float_text(hardware_messenger):
    scenarios.assert_echo_double_round_trips_as_float_text(hardware_messenger)


def test_multi_args_all_types_round_trip(hardware_messenger):
    scenarios.assert_multi_args_all_types_round_trip(hardware_messenger)


def test_unknown_command_triggers_error(hardware_messenger):
    scenarios.assert_unknown_command_triggers_error(hardware_messenger)


def test_repeated_commands_all_round_trip(hardware_messenger):
    scenarios.assert_repeated_commands_all_round_trip(hardware_messenger)
