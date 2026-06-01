"""DataLogging sample — log every received command to a file.

Pairs with ``examples/DataLogging/DataLogging.ino``.
"""
from __future__ import annotations

import time

from cmd_messenger import (
    BoardType,
    CmdMessenger,
    ReceivedCommand,
    SendCommand,
    logger,
)
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

PORT = "COM6"
BAUD = 115200
LOG_FILE = "data.log"

START_LOGGING = 0
PLOT_DATA_POINT = 1


def on_data_point(cmd: ReceivedCommand) -> None:
    t = cmd.read_float_arg()
    v = cmd.read_float_arg()
    line = f"t={t:.3f} v={v:.3f}"
    print(line)
    logger.log_line(line)


def main() -> None:
    logger.set_direct_flush(True)
    logger.open(LOG_FILE)

    transport = SerialTransport(SerialSettings(port_name=PORT, baud_rate=BAUD))
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True
    messenger.attach(PLOT_DATA_POINT, on_data_point)

    if not messenger.connect():
        print(f"Could not open {PORT}")
        logger.close()
        return

    try:
        messenger.send_command(SendCommand(START_LOGGING))
        time.sleep(30)
    except KeyboardInterrupt:
        pass
    finally:
        messenger.disconnect()
        messenger.dispose()
        logger.close()


if __name__ == "__main__":
    main()
