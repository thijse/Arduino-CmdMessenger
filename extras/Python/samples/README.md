# py-cmdmessenger samples

These samples mirror the C# examples under
`extras/CSharp/` and pair with the corresponding Arduino sketches in
`examples/`. They demonstrate the host (PC) side of the protocol.

| Python sample | Arduino sketch | Notes |
|---|---|---|
| [receive.py](receive.py) | `examples/Receive/` | Send only — toggles an LED |
| [send_and_receive.py](send_and_receive.py) | `examples/SendAndReceive/` | Send `SetLed`, receive `Status` |
| [send_and_receive_arguments.py](send_and_receive_arguments.py) | `examples/SendAndReceiveArguments/` | Multi-arg ack request/reply |
| [send_and_receive_binary_arguments.py](send_and_receive_binary_arguments.py) | `examples/SendAndReceiveBinaryArguments/` | Binary encoded floats |
| [simple_watchdog.py](simple_watchdog.py) | `examples/SimpleWatchdog/` | `SerialConnectionManager` + watchdog |
| [data_logging.py](data_logging.py) | `examples/DataLogging/` | Stream data to disk via `cmd_messenger.logger` |

## Running

Edit the `PORT` constant near the top of any sample to match your serial
device (e.g. `COM6` on Windows, `/dev/ttyUSB0` on Linux,
`/dev/cu.usbmodem14101` on macOS), then:

```powershell
cd extras\Python
.\.venv\Scripts\python.exe samples\send_and_receive.py
```

The samples use `cmd_messenger.SerialTransport` directly. If you'd rather let
the library auto-detect the port (and verify the device responds), use
`SerialConnectionManager` — see `simple_watchdog.py`.
