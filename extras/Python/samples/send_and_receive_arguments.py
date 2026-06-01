"""SendAndReceiveArguments sample — synchronous request/reply.

Sends a ``FloatAdd`` request with two operands and prints the ``FloatAddResult``
that the Arduino returns. Pairs with
``examples/SendAndReceiveArguments/SendAndReceiveArguments.ino``.
"""
from __future__ import annotations

from cmd_messenger import BoardType, CmdMessenger, SendCommand
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

PORT = "COM6"
BAUD = 115200

# Command IDs (must match the Arduino sketch enum).
ACKNOWLEDGE = 0
ERROR = 1
FLOAT_ADDITION = 2
FLOAT_ADDITION_RESULT = 3


def main() -> None:
    transport = SerialTransport(SerialSettings(port_name=PORT, baud_rate=BAUD))
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True

    if not messenger.connect():
        print(f"Could not open {PORT}")
        return

    try:
        req = SendCommand(
            FLOAT_ADDITION, 3.14, 2.71,
            ack_cmd_id=FLOAT_ADDITION_RESULT,
            timeout=1000,
        )
        reply = messenger.send_command(req)
        if reply.ok:
            result = reply.read_float_arg()
            print(f"3.14 + 2.71 = {result}")
        else:
            print("No reply (timeout)")
    finally:
        messenger.disconnect()
        messenger.dispose()


if __name__ == "__main__":
    main()
