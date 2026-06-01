"""ArduinoController — LED blink-frequency control via slider.

Port of C# ``ArduinoController.cs``. The browser shows a slider and a toggle
button. Slider events send ``SetLedFrequency`` commands wrapped in
:class:`CollapseCommandStrategy` to avoid queue lag.

Pairs with ``examples/ArduinoController/ArduinoController.ino``.
"""
from __future__ import annotations

import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from cmd_messenger import (
    BoardType,
    CmdMessenger,
    CollapseCommandStrategy,
    ReceivedCommand,
    SendCommand,
)
from cmd_messenger.transport.serial import SerialSettings, SerialTransport
from shared import WebForm

PORT = "COM6"
BAUD = 115200


# Command IDs (must match Arduino sketch enum).
class Command:
    ACKNOWLEDGE = 0
    ERROR = 1
    SET_LED = 2
    SET_LED_FREQUENCY = 3


class ArduinoController:
    def __init__(self) -> None:
        self._transport: SerialTransport
        self._messenger: CmdMessenger
        self._form: WebForm

    def setup(self, form: WebForm) -> None:
        self._form = form

        # Register browser → Python commands.
        form.on_command("set_frequency", self._on_slider_change)
        form.on_command("set_led", self._on_toggle)

        self._transport = SerialTransport(
            SerialSettings(port_name=PORT, baud_rate=BAUD)
        )
        self._messenger = CmdMessenger(self._transport, board_type=BoardType.BIT_16)

        # Attach callbacks.
        self._messenger.attach(self._on_unknown)
        self._messenger.attach(Command.ACKNOWLEDGE, self._on_acknowledge)
        self._messenger.attach(Command.ERROR, self._on_error)

        self._messenger.new_line_received += lambda cmd: self._form.log_message(f"Received > {cmd.command_string()}")
        self._messenger.new_line_sent += lambda cmd: self._form.log_message(f"Sent > {cmd.command_string()}")

        if not self._messenger.connect():
            self._form.log_message(f"Could not open {PORT}")
            return

        # Initial state.
        self.set_led_state(True)
        self.set_led_frequency(2.0)

    def exit(self) -> None:
        self._messenger.disconnect()
        self._messenger.dispose()
        self._transport.dispose()

    # ------------------------------------------------------------------
    # Commands to Arduino
    # ------------------------------------------------------------------
    def set_led_frequency(self, frequency: float) -> None:
        command = SendCommand(Command.SET_LED_FREQUENCY, frequency)
        # CollapseCommandStrategy avoids queue lag when the slider fires rapidly.
        self._messenger.queue_command(CollapseCommandStrategy(command))

    def set_led_state(self, on: bool) -> None:
        self._messenger.send_command(SendCommand(Command.SET_LED, on))

    # ------------------------------------------------------------------
    # Browser → Python handlers
    # ------------------------------------------------------------------
    def _on_slider_change(self, value) -> None:
        try:
            self.set_led_frequency(float(value))
        except (TypeError, ValueError):
            pass

    def _on_toggle(self, value) -> None:
        self.set_led_state(bool(value))

    # ------------------------------------------------------------------
    # Arduino → Python callbacks
    # ------------------------------------------------------------------
    def _on_unknown(self, cmd: ReceivedCommand) -> None:
        self._form.log_message("Command without attached callback received")

    def _on_acknowledge(self, cmd: ReceivedCommand) -> None:
        self._form.log_message("Arduino is ready")

    def _on_error(self, cmd: ReceivedCommand) -> None:
        self._form.log_message("Arduino has experienced an error")
