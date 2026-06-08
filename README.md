# CmdMessenger

<p align="center">
  <img src="extras/CmdMessenger.png" alt="CmdMessenger — Arduino to PC communication" width="100%">
</p>

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
[![Version](https://img.shields.io/badge/version-5.0.0-green.svg)](library.json)
[![Arduino Library Manager](https://img.shields.io/badge/Arduino-Library%20Manager-blue)](https://www.arduinolibraries.info/libraries/cmd-messenger)

A command-based messaging library for Arduino that enables structured communication between microcontrollers and PC host applications over serial, Bluetooth, or TCP/IP.

The protocol is simple — `<cmdId>,<arg1>,<arg2>,...;` — but the library handles typed arguments, binary encoding, callback dispatch, send/receive queues, connection management, and auto-reconnect for you.

---

## Host client libraries

CmdMessenger includes first-class host-side clients for three languages. Pick the one that fits your project.

| Language | Location | Status | Transports |
|----------|----------|--------|-----------|
| **C# / .NET** | [`extras/CSharp/`](extras/CSharp/) | ✅ Stable | Serial, TCP, Bluetooth (Windows) |
| **Python** | [`extras/Python/`](extras/Python/) | 🚧 Alpha | Serial, TCP |
| **TypeScript / Node.js** | [`extras/TypeScript/`](extras/TypeScript/) | 🚧 Initial | Serial, TCP |

All three clients share the same protocol, the same queue strategies, and the same test scenarios — a sketch that works with the C# client will work unchanged with the Python or TypeScript client.

---

## Install the Arduino library

**Arduino Library Manager** (recommended):

Search for "CmdMessenger" in *Sketch → Include Library → Manage Libraries*.

**PlatformIO:**

```ini
lib_deps = CmdMessenger
```

---

## Arduino quickstart

```cpp
#include <CmdMessenger.h>

enum { kSetLed, kStatus };

CmdMessenger cmdMessenger = CmdMessenger(Serial);

void onSetLed() {
  bool ledState = cmdMessenger.readBoolArg();
  digitalWrite(LED_BUILTIN, ledState ? HIGH : LOW);
  cmdMessenger.sendCmd(kStatus, ledState);
}

void setup() {
  Serial.begin(115200);
  cmdMessenger.attach(kSetLed, onSetLed);
  pinMode(LED_BUILTIN, OUTPUT);
}

void loop() {
  cmdMessenger.feedinSerialData();
}
```

Send `0,1;` from the Serial Monitor → LED on, board replies `1,1;`.

---

## C# client

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download) or later. Works on Windows, Linux and macOS.

```csharp
using CommandMessenger;
using CommandMessenger.Transport.Serial;

var transport = new SerialTransport
{
    CurrentSerialSettings = { PortName = "COM3", BaudRate = 115200, DtrEnable = true }
};
var messenger = new CmdMessenger(transport);
messenger.Connect();

messenger.Attach(1 /* kStatus */, cmd =>
    Console.WriteLine("LED is now " + (cmd.ReadBoolArg() ? "on" : "off")));

// Send command 0 (kSetLed) with argument true, wait for ACK command 1
var ack = await messenger.SendCommandAsync(new SendCommand(0, true, 1, 500));
```

**Run the paired samples:**

```bash
cd extras/CSharp/Samples/SendAndReceive
dotnet run
```

The sample projects are in [`extras/CSharp/Samples/`](extras/CSharp/Samples/) — numbered 1–10 matching the Arduino examples.

**UI thread marshalling** — if calling from a WinForms/WPF/MAUI UI thread, set:

```csharp
messenger.SynchronizationContext = SynchronizationContext.Current;
```

This routes all callbacks to the UI thread automatically.

---

## Python client

Requires Python 3.9+. Works on Windows, Linux and macOS.

```bash
cd extras/Python
build.bat          # Windows
bash build.sh      # macOS / Linux
```

This creates a `.venv`, installs dependencies, and installs `py-cmdmessenger` in editable mode.

```python
from cmd_messenger import CmdMessenger, SendCommand, BoardType
from cmd_messenger.transport.serial import SerialTransport, SerialSettings

settings = SerialSettings(port_name="COM3", baud_rate=115200)
transport = SerialTransport(settings)

with CmdMessenger(transport, board_type=BoardType.BIT_16) as messenger:
    messenger.connect()

    def on_status(cmd):
        print("LED is now", cmd.read_bool_arg())

    messenger.attach(1, on_status)
    messenger.send_command(SendCommand(0, True))  # kSetLed, true
    import time; time.sleep(0.5)
```

See [`extras/Python/`](extras/Python/) for the full library and console + web-UI samples.

---

## TypeScript / Node.js client

Requires Node.js 22+.

```bash
cd extras/TypeScript
npm ci
npm run build
```

```typescript
import { BoardType, CmdMessenger, SendCommand } from 'cmd-messenger';
import { SerialTransport, SerialSettings } from 'cmd-messenger/transport/serial';

const transport = new SerialTransport(new SerialSettings({ portName: 'COM3', baudRate: 115200 }));
const messenger = new CmdMessenger(transport, BoardType.Bit16);

await messenger.connect();
messenger.attach(1 /* kStatus */, cmd =>
    console.log('LED is now', cmd.readBoolArg()));

await messenger.sendCommand(new SendCommand(0, true));  // kSetLed, true
```

See [`extras/TypeScript/`](extras/TypeScript/) for the full library.

---

## Arduino features

- **All primary types** — bool, int16, int32, float, double, char, string, binary
- **Plain text or binary encoding** — human-readable or efficient, your choice
- **Callback-driven** — `attach(cmdId, handler)` for each command ID
- **`sendArg()` family** — send arguments without field separator for custom packet formats
- **Configurable delimiters** — field separator (`,`) and command separator (`;`) are settable
- **Multiple transports** — Serial USB, Bluetooth (HC-05/HC-06), TCP over WiFi/Ethernet
- **Any `Stream` interface** — works with `Serial`, `SoftwareSerial`, `WiFiClient`, BLE UART, etc.

## Compatibility

| Platform | Notes |
|---------|-------|
| Arduino AVR (Uno, Mega, Nano, Pro Micro…) | Full support |
| Arduino ARM (Due, Zero) | Full support |
| ESP8266 / ESP32 | Full support; use `RtsEnable = true` on the C# side for auto-reset |
| Teensy 3.x / 4.x | Full support |
| Digispark (attiny85) | Supported; requires Energia or Digispark core |
| Any `Stream`-compatible board | Should work |

Requires Arduino IDE 1.5+ or PlatformIO.

---

## Examples

| Sketch | Demonstrates | C# | Python | TypeScript |
|--------|-------------|----|----|----------|
| [Receive](examples/Receive/) | Receive commands | ✅ | ✅ | ✅ |
| [SendAndReceive](examples/SendAndReceive/) | Bidirectional | ✅ | ✅ | ✅ |
| [SendAndReceiveArguments](examples/SendAndReceiveArguments/) | Typed arguments | ✅ | ✅ | ✅ |
| [SendAndReceiveBinaryArguments](examples/SendAndReceiveBinaryArguments/) | Binary encoding | ✅ | ✅ | ✅ |
| [DataLogging](examples/DataLogging/) | Live charting | ✅ | ✅ | — |
| [ArduinoController](examples/ArduinoController/) | GUI + queued commands | ✅ | ✅ | — |
| [TemperatureControl](examples/TemperatureControl/) | PID + live chart | ✅ | ✅ | — |
| [SimpleWatchdog](examples/SimpleWatchdog/) | Auto-reconnect | ✅ | ✅ | ✅ |
| [ConsoleShell](examples/ConsoleShell/) | Serial shell (no PC app) | — | — | — |
| [SendWithoutSeparator](examples/SendWithoutSeparator/) | Custom packet format | — | — | — |

---

## Troubleshooting

| Symptom | Fix |
|--------|-----|
| Cannot connect | Verify COM port and baud rate. Test with the ConsoleShell sketch + Serial Monitor. |
| Callbacks not firing | Enable logging on the C# side; check `DtrEnable = true` for boards that need DTR reset. |
| ESP32 / ESP8266 not resetting | Set both `DtrEnable = true` and `RtsEnable = true` on the C# / Python side. |
| PlatformIO install fails | Ensure your `lib_deps` uses `CmdMessenger` (no extra dependencies are required). |
| Pro Micro (32u4) | Native USB — set `DtrEnable = true`. |

---

## Credits

| Version | Author |
|---------|--------|
| Messenger (original) | Thomas Ouellet Fredericks |
| CmdMessenger v1 | Neil Dudman |
| CmdMessenger v2 | Dreamcat4 |
| CmdMessenger v3–v4 | Thijs Elenbaas & Valeriy Kucherenko |
| CmdMessenger v5 | Thijs Elenbaas |

---

## License

[MIT](LICENSE.md) — Copyright © 2013–2026 Thijs Elenbaas.

---

<details>
<summary>Changelog</summary>

### v5.0.0

**New host clients:**
- [Python] First-class `py-cmdmessenger` library — full parity with C# (serial + TCP, all queue strategies, connection manager, watchdog, auto-reconnect, `JsonSerialConnectionStorer`)
- [TypeScript] First-class Node.js client — serial + TCP, all queue strategies, connection manager, `JsonSerialConnectionStorer`

**C# — async core rewrite:**
- `SendCommandAsync(cmd)` / `await messenger.SendCommandAsync(...)` — proper async ACK wait via `TaskCompletionSource` per pending ACK; no busy-spin, no receive-queue suspension
- `AsyncWorker` replaced with `async Task` drain loop + `CancellationTokenSource`
- `EventWaiter` replaced with `SemaphoreSlim`-based async waiter
- `System.Threading.Channels` used for queue backing

**C# — modernisation:**
- Target framework: `netstandard2.0` (covers .NET Framework 4.6.1+, .NET Core 2+, .NET 5–11, Linux, macOS)
- `System.Windows.Forms` dependency removed from core library
- `ControlToInvokeOn` (WinForms-only) replaced with `SynchronizationContext` — works with WinForms, WPF, MAUI, console, any platform
- All project files migrated from old verbose MSBuild format to SDK-style
- Console samples target `net8.0`; WinForms samples target `net8.0-windows`
- `BinaryFormatter` in storers replaced with JSON (`JsonSerialConnectionStorer`)
- `ScottPlot 5` replaces `ZedGraph` in charting samples
- Callback exceptions now isolated — unhandled exceptions fire `CallbackException` event instead of crashing the receive pump
- `ReadCharArg()` added to `ReceivedCommand`
- `Read(string format)` format-string reader added (e.g. `cmd.Read("ifs")`)
- `IEnumerable<string>` added to `ReceivedCommand` (foreach over raw arguments)
- `RtsEnable` added to `SerialSettings` (ESP32/ESP8266 auto-reset)
- `SerialConnectionStorer` now persists to JSON; `BinaryFormatter` removed
- 247 xUnit unit tests (was 98)

**Arduino firmware:**
- `comms->print(cmdId, DEC)` — fixes compile error on Digispark/Energia non-standard cores
- `CMDMESSENGER_MAXCALLBACKS` used consistently throughout

**Bug fixes:**
- [C#/#22] Commands arriving during ACK wait were silently dropped — fixed; non-ACK commands always queued
- [C#/#29] `ObjectDisposedException` on sleep/wake / hot-unplug — fixed via proper disposal order
- [C#/#30] Receive queue not signalled after suspend/resume — fixed

**Metadata:**
- `library.properties` version aligned with `library.json`
- Spurious `Adafruit MAX31855 library` dependency removed from `library.json`

### v4.1.0
* [Arduino] `sendArg()`, `sendSciArg()`, `sendBinArg()` — send arguments without field separator
* [Arduino] `unescape()` applied to string reads (fixes escaped delimiter handling)
* [Arduino] Bounds-checked `readBinArg<T>()` with `ArgOk` validation
* [Arduino] Configurable `CMDMESSENGER_MAXCALLBACKS` with compile-time zero-callback support
* [Arduino] ESP8266/ESP32 type fixes
* [Arduino] `LastArgLength` tracking in argument parser
* Fix: `startCommand` initialization
* Fix: `comms->print` used consistently (no direct `Serial.print`)
* Fix: LICENSE.md reformatted for GitHub detection

### v4.0.0
* [Arduino] Additional autoConnect sample
* [.NET] Full threading redesign
* [.NET] AutoConnect & watchdog functionality
* [.NET] Tested Linux compatibility
* [.NET] Visual Basic samples
* [.NET] Native Bluetooth support (Windows only)

### v3.6
* [Arduino] Bugfix: ~1 in 1000 commands failed with multiple binary parameters
* [Arduino] Bugfix: binary send of non-number compile error
* [Arduino] Feature: send command without argument
* [Arduino] Feature: scientific notation for full float range
* [.NET] Unit tests, performance improvements

### v3.5
* [Arduino] Console shell sample, minor performance improvement

### v3.4
* [Arduino] Bug-fix in receiving binary values
* [.NET] 100× faster communication, single-core support

### v3.3
* [Arduino] Speed improvements for Teensy

### v3.2
* [All] Clean transport layer interface (Bluetooth, ZigBee, Web)
* [.NET] Adaptive throttling, smart queuing

### v3.0
* Multi-argument commands, type arguments, binary data, escaping, acknowledgements

### v2
* Arduino IDE 022, configurable separators, Base-64 encoding, Serial Monitor debugging

</details>
