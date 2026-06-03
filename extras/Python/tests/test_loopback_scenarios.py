"""Transport-agnostic loopback scenarios against in-process simulated firmware."""
from __future__ import annotations

from cmd_messenger import CmdMessenger
from cmd_messenger.enums import BoardType

from .firmware_simulator import LoopbackFirmware, SimulatedFirmwareTransport
from . import loopback_scenarios as scenarios


class TestSimulatedLoopbackScenarios:
    def setup_method(self):
        self.transport = SimulatedFirmwareTransport(LoopbackFirmware().handle)
        self.messenger = CmdMessenger(self.transport, board_type=BoardType.BIT_16)
        self.transport.connect()

    def teardown_method(self):
        self.messenger.dispose()
        self.transport.dispose()

    def test_ping_returns_pong(self):
        scenarios.assert_ping_returns_pong(self.messenger)

    def test_echo_string_round_trips(self):
        scenarios.assert_echo_string_round_trips(self.messenger)

    def test_echo_string_with_special_chars_round_trips(self):
        scenarios.assert_echo_string_with_special_chars_round_trips(self.messenger)

    def test_add_floats_returns_sum_and_difference(self):
        scenarios.assert_add_floats_returns_sum_and_difference(self.messenger)

    def test_echo_int32_round_trips(self):
        scenarios.assert_echo_int32_round_trips(self.messenger)

    def test_echo_int16_round_trips(self):
        scenarios.assert_echo_int16_round_trips(self.messenger)

    def test_echo_bool_round_trips(self):
        scenarios.assert_echo_bool_round_trips(self.messenger)

    def test_echo_double_round_trips_as_float_text(self):
        scenarios.assert_echo_double_round_trips_as_float_text(self.messenger)

    def test_multi_args_all_types_round_trip(self):
        scenarios.assert_multi_args_all_types_round_trip(self.messenger)

    def test_unknown_command_triggers_error(self):
        scenarios.assert_unknown_command_triggers_error(self.messenger)

    def test_repeated_commands_all_round_trip(self):
        scenarios.assert_repeated_commands_all_round_trip(self.messenger)
