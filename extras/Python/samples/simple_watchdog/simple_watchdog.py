"""SimpleWatchdog sample — auto-connect and supervise a device.

Pairs with ``examples/SimpleWatchdog/SimpleWatchdog.ino``. The sketch responds
to the identify command (id 0) with its unique id string.
"""
from __future__ import annotations

import time

from cmd_messenger import (
    BoardType,
    CmdMessenger,
    ConnectionManagerProgressEventArgs,
    SerialConnectionManager,
)
from cmd_messenger.transport.serial import SerialSettings, SerialTransport

BAUD = 115200
UNIQUE_DEVICE_ID = "BFAF4176-766E-436A-ADF2-96133C02B03C"  # match the sketch
IDENTIFY_CMD_ID = 0


def on_progress(args: ConnectionManagerProgressEventArgs) -> None:
    print(f"  [{args.level}] {args.description}")


def main() -> None:
    transport = SerialTransport(SerialSettings(baud_rate=BAUD))
    messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
    messenger.print_lf_cr = True

    connmgr = SerialConnectionManager(
        transport,
        messenger,
        watchdog_command_id=IDENTIFY_CMD_ID,
        unique_device_id=UNIQUE_DEVICE_ID,
    )
    connmgr.progress += on_progress
    connmgr.connection_found += lambda: print("** Device found **")
    connmgr.connection_timeout += lambda: print("** Device timed out **")
    connmgr.watchdog_enabled = True

    try:
        connmgr.start_connection_manager()
        # Run for 30 seconds.
        time.sleep(30)
    except KeyboardInterrupt:
        pass
    finally:
        connmgr.stop_connection_manager()
        messenger.dispose()


if __name__ == "__main__":
    main()
