"""DataLogging (Web) — real-time chart of data points from Arduino.

Port of C# ``DataLogging.cs`` + ``ChartForm.cs``. The browser shows a rolling
Plotly chart of (time, value) data points streamed from the Arduino, plus a log.

Pairs with ``examples/DataLogging/DataLogging.ino``.
"""
from __future__ import annotations

import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from cmd_messenger import (
    BoardType,
    CmdMessenger,
    ReceivedCommand,
    SendCommand,
)
from cmd_messenger.transport.serial import SerialSettings, SerialTransport
from shared import WebForm

PORT = "COM6"
BAUD = 115200


class Command:
    START_LOGGING = 0
    PLOT_DATA_POINT = 1


class DataLogging:
    def __init__(self) -> None:
        self._transport: SerialTransport
        self._messenger: CmdMessenger
        self._form: WebForm

    def setup(self, form: WebForm) -> None:
        self._form = form

        # Register browser commands.
        form.on_command("start", lambda v: self._start_logging())
        form.on_command("stop", lambda v: self._stop_logging())

        self._transport = SerialTransport(
            SerialSettings(port_name=PORT, baud_rate=BAUD)
        )
        self._messenger = CmdMessenger(self._transport, board_type=BoardType.BIT_16)
        self._messenger.print_lf_cr = True

        # Callbacks.
        self._messenger.attach(Command.PLOT_DATA_POINT, self._on_data_point)
        self._messenger.new_line_received += lambda cmd: self._form.log_message(
            f"Received > {cmd.command_string()}"
        )
        self._messenger.new_line_sent += lambda cmd: self._form.log_message(
            f"Sent > {cmd.command_string()}"
        )

        if not self._messenger.connect():
            self._form.log_message(f"Could not open {PORT}")
            return

        self._form.log_message(f"Connected to {PORT}")
        self._start_logging()

    def exit(self) -> None:
        self._messenger.disconnect()
        self._messenger.dispose()
        self._transport.dispose()

    # ------------------------------------------------------------------
    # Commands
    # ------------------------------------------------------------------
    def _start_logging(self) -> None:
        self._messenger.send_command(SendCommand(Command.START_LOGGING))
        self._form.log_message("Logging started")

    def _stop_logging(self) -> None:
        # Just disconnect/reconnect in the simple example.
        self._form.log_message("Logging stopped (send Ctrl+C to exit)")

    # ------------------------------------------------------------------
    # Arduino callback
    # ------------------------------------------------------------------
    def _on_data_point(self, cmd: ReceivedCommand) -> None:
        t = cmd.read_float_arg()
        v = cmd.read_float_arg()
        self._form.update_chart(time=t, value=v)
