# py-cmdmessenger samples

These samples mirror the C# examples under
`extras/CSharp/` and pair with the corresponding Arduino sketches in
`examples/`. They demonstrate the host (PC) side of the protocol.

| Python sample | Arduino sketch | Notes |
|---|---|---|
| [receive/](receive/) | `examples/Receive/` | Send only — toggles an LED |
| [send_and_receive/](send_and_receive/) | `examples/SendAndReceive/` | Send `SetLed`, receive `Status` |
| [send_and_receive_arguments/](send_and_receive_arguments/) | `examples/SendAndReceiveArguments/` | Multi-arg ack request/reply |
| [send_and_receive_binary_arguments/](send_and_receive_binary_arguments/) | `examples/SendAndReceiveBinaryArguments/` | Binary encoded floats |
| [data_logging/](data_logging/) | `examples/DataLogging/` | Real-time chart via web UI + console variant |
| [arduino_controller/](arduino_controller/) | `examples/ArduinoController/` | Slider + toggle via web UI |
| [simple_watchdog/](simple_watchdog/) | `examples/SimpleWatchdog/` | `SerialConnectionManager` + watchdog |
| [temperature_control/](temperature_control/) | `examples/TemperatureControl/` | Full: chart + slider + ConnectionManager |

## Running

Edit the `PORT` constant near the top of any sample to match your serial
device (e.g. `COM6` on Windows, `/dev/ttyUSB0` on Linux,
`/dev/cu.usbmodem14101` on macOS), then:

```powershell
cd extras\Python
.\.venv\Scripts\python.exe samples\send_and_receive\send_and_receive.py
```

The samples use `cmd_messenger.SerialTransport` directly. If you'd rather let
the library auto-detect the port (and verify the device responds), use
`SerialConnectionManager` — see `simple_watchdog/simple_watchdog.py`.
