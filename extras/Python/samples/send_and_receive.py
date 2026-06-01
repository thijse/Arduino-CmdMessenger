"""SendAndReceive sample — toggle an LED and print Arduino status.

Pairs with ``examples/SendAndReceive/SendAndReceive.ino``.
"""
from __future__ import annotations

import time

from cmd_messenger import BoardType, CmdMessenger, ReceivedCommand, SendCommand
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

PORT = "COM6"
BAUD = 115200


# Command IDs (Arduino enum order).
SET_LED = 0
STATUS = 1


def on_status(cmd: ReceivedCommand) -> None:
    print(f"Arduino status: {cmd.read_string_arg()}")


def on_unknown(cmd: ReceivedCommand) -> None:
    print(f"Command without attached callback received: {cmd.cmd_id}")


def main() -> None:
    transport = SerialTransport(SerialSettings(port_name=PORT, baud_rate=BAUD))
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True

    messenger.attach(on_unknown)
    messenger.attach(STATUS, on_status)

    if not messenger.connect():
        print(f"Could not open {PORT}")
        return

    try:
        led_state = False
        for _ in range(100):
            messenger.send_command(SendCommand(SET_LED, led_state))
            led_state = not led_state
            time.sleep(1.0)
    finally:
        messenger.disconnect()
        messenger.dispose()


if __name__ == "__main__":
    main()
