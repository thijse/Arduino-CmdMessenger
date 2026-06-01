"""SendAndReceiveBinaryArguments sample — binary float round-trip.

Pairs with
``examples/SendAndReceiveBinaryArguments/SendAndReceiveBinaryArguments.ino``.
"""
from __future__ import annotations

from cmd_messenger import BoardType, CmdMessenger, SendCommand
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

PORT = "COM6"
BAUD = 115200

FLOAT_ADDITION = 2
FLOAT_ADDITION_RESULT = 3


def main() -> None:
    transport = SerialTransport(SerialSettings(port_name=PORT, baud_rate=BAUD))
    # Use BIT_16 for AVR (Uno), BIT_32 for ARM (Due/Teensy/ESP).
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)

    if not messenger.connect():
        print(f"Could not open {PORT}")
        return

    try:
        req = SendCommand(
            FLOAT_ADDITION,
            ack_cmd_id=FLOAT_ADDITION_RESULT,
            timeout=1000,
        )
        # Add as raw binary instead of text — saves bytes on the wire.
        req.add_bin_argument(3.14)
        req.add_bin_argument(2.71)

        reply = messenger.send_command(req)
        if reply.ok:
            print(f"3.14 + 2.71 = {reply.read_bin_float_arg()}")
        else:
            print("No reply")
    finally:
        messenger.disconnect()
        messenger.dispose()


if __name__ == "__main__":
    main()
