"""TemperatureControl — full-featured sample with charting, watchdog, and connection manager.

Port of C# ``TemperatureControl.cs``. The browser shows a real-time Plotly chart
of temperature/heater data plus a goal-temperature slider.

Uses:
- SerialConnectionManager (auto-scan, reconnect, watchdog)
- StaleGeneralStrategy(1000) to drop stale data from receive queue
- CollapseCommandStrategy for rapid goal-temperature slider events
- Binary float arguments in both directions

Pairs with ``examples/TemperatureControl/TemperatureControl.ino``.
"""
from __future__ import annotations

import sys, os, time
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from cmd_messenger import (
    BoardType,
    CmdMessenger,
    CollapseCommandStrategy,
    ReceivedCommand,
    SendCommand,
    SendQueue,
    ReceiveQueue,
    UseQueue,
)
from cmd_messenger.queue import StaleGeneralStrategy
from cmd_messenger.transport.serial import SerialSettings, SerialTransport
from cmd_messenger.connection_manager import ConnectionManager as _ConnectionManagerBase
from cmd_messenger.transport.serial import SerialConnectionManager
from cmd_messenger.time_utils import millis
from shared import WebForm


UNIQUE_DEVICE_ID = "77FAEDD5-FAC8-46BD-875E-5E9B6D44F85C"


class Command:
    IDENTIFY = 0
    ACKNOWLEDGE = 1
    ERROR = 2
    START_LOGGING = 3
    STOP_LOGGING = 4
    PLOT_DATA_POINT = 5
    SET_GOAL_TEMPERATURE = 6
    SET_START_TIME = 7


class TemperatureControl:
    def __init__(self) -> None:
        self._transport: SerialTransport
        self._messenger: CmdMessenger
        self._connection_manager: SerialConnectionManager
        self._form: WebForm
        self._goal_temperature: float = 25.0
        self._start_time: int = 0
        self.acquisition_started: bool = False
        self.accept_data: bool = False

    # ------------------------------------------------------------------
    # Setup / Exit
    # ------------------------------------------------------------------
    def setup(self, form: WebForm) -> None:
        self._form = form
        self._start_time = millis()

        # Register browser → Python commands.
        form.on_command("set_goal_temperature", self._on_slider_change)
        form.on_command("start_acquisition", lambda v: self.start_acquisition())
        form.on_command("stop_acquisition", lambda v: self.stop_acquisition())

        # Transport (serial — port/baud discovered by connection manager).
        self._transport = SerialTransport(
            SerialSettings(baud_rate=115200, dtr_enable=False)
        )

        # CmdMessenger (Bit32 board).
        self._messenger = CmdMessenger(self._transport, board_type=BoardType.BIT_32)
        self._messenger.print_lf_cr = False

        # Stale strategy: drop data older than 1s so the chart doesn't lag.
        self._messenger.add_receive_command_strategy(StaleGeneralStrategy(1000))

        # Callbacks.
        self._attach_callbacks()
        self._messenger.new_line_received += lambda cmd: self._form.log_message(
            f"Received > {cmd.command_string()}"
        )
        self._messenger.new_line_sent += lambda cmd: self._form.log_message(
            f"Sent > {cmd.command_string()}"
        )

        # Connection manager with watchdog.
        self._connection_manager = SerialConnectionManager(
            self._transport,
            self._messenger,
            Command.IDENTIFY,
            UNIQUE_DEVICE_ID,
        )
        self._connection_manager.watchdog_enabled = True
        self._connection_manager.connection_found += self._connection_found
        self._connection_manager.connection_timeout += self._connection_timeout
        self._connection_manager.progress += self._log_progress

        # Initial UI state.
        self._form.set_status("Disconnected — scanning for device…")
        self._form.send_to_clients({
            "type": "state",
            "connected": False,
            "goal_temperature": self._goal_temperature,
        })

        # Start scanning.
        self._connection_manager.start_connection_manager()

    def exit(self) -> None:
        self._connection_manager.progress -= self._log_progress
        self._connection_manager.connection_timeout -= self._connection_timeout
        self._connection_manager.connection_found -= self._connection_found
        self._connection_manager.dispose()
        self._messenger.disconnect()
        self._messenger.dispose()
        self._transport.dispose()

    # ------------------------------------------------------------------
    # Callbacks
    # ------------------------------------------------------------------
    def _attach_callbacks(self) -> None:
        self._messenger.attach(self._on_unknown)
        self._messenger.attach(Command.ACKNOWLEDGE, self._on_acknowledge)
        self._messenger.attach(Command.ERROR, self._on_error)
        self._messenger.attach(Command.PLOT_DATA_POINT, self._on_plot_data_point)

    def _on_unknown(self, cmd: ReceivedCommand) -> None:
        self._form.log_message("Command without attached callback received")

    def _on_acknowledge(self, cmd: ReceivedCommand) -> None:
        self._form.log_message("Arduino acknowledged")

    def _on_error(self, cmd: ReceivedCommand) -> None:
        self._form.log_message("Arduino has experienced an error")

    def _on_plot_data_point(self, cmd: ReceivedCommand) -> None:
        if not self.accept_data:
            return
        # Read binary float arguments sent by the Arduino.
        _time_raw = cmd.read_bin_float_arg()
        # Use local time instead (same as C# code).
        t = (millis() - self._start_time) / 1000.0
        curr_temp = cmd.read_bin_float_arg()
        goal_temp = cmd.read_bin_float_arg()
        heater_value = cmd.read_bin_float_arg()
        heater_pwm = cmd.read_bin_bool_arg()

        self._form.update_chart(
            time=t,
            current_temperature=curr_temp,
            goal_temperature=goal_temp,
            heater_value=heater_value,
            heater_pwm=1.0 if heater_pwm else 0.0,
        )

    # ------------------------------------------------------------------
    # Connection manager events
    # ------------------------------------------------------------------
    def _log_progress(self, description: str, level: int) -> None:
        if level <= 2:
            self._form.set_status(description)
        self._form.log_message(description)

    def _connection_found(self) -> None:
        self.accept_data = False
        self._form.set_status("Connected")
        self._form.send_to_clients({"type": "state", "connected": True})
        self.set_goal_temperature(self._goal_temperature)
        if self.acquisition_started:
            self.start_acquisition()
        else:
            self.stop_acquisition()
        self.accept_data = True

    def _connection_timeout(self) -> None:
        self._form.set_status("Connection timeout — attempting to reconnect")
        self._form.send_to_clients({"type": "state", "connected": False})

    # ------------------------------------------------------------------
    # Commands to Arduino
    # ------------------------------------------------------------------
    def set_goal_temperature(self, temperature: float) -> None:
        self._goal_temperature = temperature
        command = SendCommand(Command.SET_GOAL_TEMPERATURE)
        command.add_bin_argument(float(temperature))
        self._form.log_message("Queue command — SetGoalTemperature")
        self._messenger.queue_command(CollapseCommandStrategy(command))

    def set_start_time(self, start_time: float) -> None:
        command = SendCommand(
            Command.SET_START_TIME, ack_cmd_id=Command.ACKNOWLEDGE, timeout=500
        )
        command.add_bin_argument(float(start_time))
        self._messenger.send_command(
            command,
            send_queue=SendQueue.CLEAR_QUEUE,
            receive_queue=ReceiveQueue.CLEAR_QUEUE,
            use_queue=UseQueue.BYPASS_QUEUE,
        )

    def start_acquisition(self) -> bool:
        command = SendCommand(
            Command.START_LOGGING, ack_cmd_id=Command.ACKNOWLEDGE, timeout=500
        )
        self._form.log_message("Send command — Start acquisition")
        result = self._messenger.send_command(
            command,
            send_queue=SendQueue.CLEAR_QUEUE,
            receive_queue=ReceiveQueue.CLEAR_QUEUE,
        )
        if result.ok:
            self.acquisition_started = True
        else:
            self._form.log_message("Failure > no OK received from controller")
        return result.ok

    def stop_acquisition(self) -> bool:
        command = SendCommand(
            Command.STOP_LOGGING, ack_cmd_id=Command.ACKNOWLEDGE, timeout=2500
        )
        self._form.log_message("Send command — Stop acquisition")
        result = self._messenger.send_command(
            command,
            send_queue=SendQueue.CLEAR_QUEUE,
            receive_queue=ReceiveQueue.CLEAR_QUEUE,
        )
        if result.ok:
            self.acquisition_started = False
        else:
            self._form.log_message("Failure > no OK received from controller")
        return result.ok

    # ------------------------------------------------------------------
    # Browser → Python handlers
    # ------------------------------------------------------------------
    def _on_slider_change(self, value) -> None:
        try:
            self.set_goal_temperature(float(value))
        except (TypeError, ValueError):
            pass
