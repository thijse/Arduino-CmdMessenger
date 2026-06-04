"""Hardware-in-the-loop host-to-embedded tests.

These tests require a board running ``test/integration/sketch/src/LoopbackTestRunner.ino``.
They are skipped unless pytest is run with ``--hardware``.

Board discovery mirrors C# ``BoardDiscovery.cs``:
  - Phase 1: USB VID:PID matching (Teensy, ESP32-S3)
  - Phase 2: Serial kWhoAmI query for CH340 boards (Nano, ESP8266)

Legacy mode: provide ``CMDMESSENGER_PORT`` or ``CMDMSG_HW_PORT`` env var.

Run all hardware tests:
    pytest --hardware -m hardware

Run tests for a single board:
    pytest --hardware -k "NANO"
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
def hardware_messenger(hardware_board, serial_baud):
    """Create a messenger for a discovered board.

    Mirrors C# HardwareTestBase: higher timeouts, boot-ack with ping fallback
    for boards that don't DTR-reset (e.g. Teensy).
    """
    model, port = hardware_board
    settings = SerialSettings(
        port_name=port,
        baud_rate=serial_baud,
        timeout=3000,
        dtr_enable=True,
        rts_enable=True,
    )
    transport = SerialTransport(settings)
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True

    ready = threading.Event()
    messenger.attach(LoopbackCommand.ACKNOWLEDGE, lambda _cmd: ready.set())

    assert messenger.connect()

    # Boards that reset on DTR (AVR, ESP) send boot ack on serial open.
    # Boards that don't (Teensy) need a ping/pong fallback.
    if not ready.wait(8.0):
        reply = messenger.send_command(
            SendCommand(LoopbackCommand.PING, ack_cmd_id=LoopbackCommand.PONG, timeout=3000)
        )
        assert reply.ok, (
            f"Board '{model}' on {port} did not send boot ack and did not respond to ping. "
            "Is the firmware running?"
        )

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
