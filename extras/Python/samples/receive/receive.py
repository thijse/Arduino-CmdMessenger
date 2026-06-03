"""Receive sample — sends ``SetLed`` to toggle the on-board LED.

Pairs with ``examples/Receive/Receive.ino``.
"""
from __future__ import annotations

import time

from cmd_messenger import BoardType, CmdMessenger, SendCommand
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

PORT = "COM6"
BAUD = 115200


# Command IDs — must match the order in the Arduino sketch enum.
SET_LED = 0


def main() -> None:
    transport = SerialTransport(SerialSettings(port_name=PORT, baud_rate=BAUD))
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True

    if not messenger.connect():
        print(f"Could not open {PORT}")
        return

    try:
        led_state = False
        for _ in range(20):
            messenger.send_command(SendCommand(SET_LED, led_state))
            led_state = not led_state
            time.sleep(1.0)
    finally:
        messenger.disconnect()
        messenger.dispose()


if __name__ == "__main__":
    main()
