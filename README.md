# CmdMessenger

<p align="center">
  <img src="extras/CmdMessenger.png" alt="CmdMessenger — Arduino to PC communication" width="100%">
</p>

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
[![Version](https://img.shields.io/badge/version-4.1.0-green.svg)](library.json)

A command-based messaging library for Arduino that enables structured communication between microcontrollers and PC applications over serial, Bluetooth, or TCP/IP.

**[Documentation](docs/documentation.md)** · **[Examples](examples/)** · **[C# Client](extras/CSharp/)** · **[PyCmdMessenger (Python)](https://github.com/harmsm/PyCmdMessenger)**

## Install

**Arduino Library Manager** (recommended):

Search for "CmdMessenger" in *Sketch → Include Library → Manage Libraries*.

**PlatformIO:**

```ini
lib_deps = CmdMessenger
```

## Quickstart

```cpp
#include <CmdMessenger.h>

enum { kSetLed, kStatus };

CmdMessenger cmdMessenger = CmdMessenger(Serial);

void onSetLed(void) {
  bool ledState = cmdMessenger.readBoolArg();
  digitalWrite(13, ledState ? HIGH : LOW);
  cmdMessenger.sendCmd(kStatus, ledState);
}

void setup() {
  Serial.begin(115200);
  cmdMessenger.attach(kSetLed, onSetLed);
  pinMode(13, OUTPUT);
}

void loop() {
  cmdMessenger.feedinSerialData();
}
```

Send `0,1;` from the serial monitor to turn on the LED; it responds with `1,1;`.

## Features

- **Send and receive commands** with zero or more typed arguments
- **All primary data types** — bool, int16, int32, float, double, char, string, binary
- **Plain text or binary encoding** — human-readable or efficient, your choice
- **Callback-driven** — attach handler functions to command IDs
- **Separator-free arguments** — `sendArg()` family for custom packet formats
- **Configurable delimiters** — field separator (`,`) and command separator (`;`) are changeable
- **Multiple transports** — Serial USB, Bluetooth, TCP/IP
- **PC companion libraries** — full C# (.NET) and Visual Basic implementations included

## Compatibility

- Arduino AVR (Uno, Mega, Nano, Pro Micro, etc.)
- Arduino ARM (Due, Zero)
- ESP8266 and ESP32
- Teensy 3.x / 4.x
- Any board with a `Stream`-compatible interface

Requires Arduino IDE 1.5+ or PlatformIO.

## Examples

The [examples/](examples/) folder contains sketches from basic to advanced, each with a matching C# PC counterpart:

| Example | Demonstrates |
|---------|-------------|
| [Receive](examples/Receive/) | Receive commands with parameters |
| [SendAndReceive](examples/SendAndReceive/) | Bidirectional communication |
| [SendAndReceiveArguments](examples/SendAndReceiveArguments/) | Multiple typed arguments |
| [SendAndReceiveBinaryArguments](examples/SendAndReceiveBinaryArguments/) | Efficient binary encoding |
| [SendWithoutSeparator](examples/SendWithoutSeparator/) | `sendArg()` family for custom formats |
| [DataLogging](examples/DataLogging/) | Charting with ZedGraph |
| [ArduinoController](examples/ArduinoController/) | GUI control with queued commands |
| [TemperatureControl](examples/TemperatureControl/) | PID control with live charting |
| [SimpleWatchdog](examples/SimpleWatchdog/) | Auto-reconnect over serial |
| [ConsoleShell](examples/ConsoleShell/) | Interactive serial shell (no PC app) |

### Running the paired examples

1. Upload the `.ino` sketch to your board.
2. Open `extras/CSharp/CmdMessenger.sln` in Visual Studio.
3. Set the matching project as startup, then run.

## Troubleshooting

- **Cannot connect** — verify COM port and baud rate match. Use the ConsoleShell example with the Arduino Serial Monitor to test.
- **Callbacks not firing** — enable logging; see the SendAndReceiveArguments example.
- **Sparkfun Pro Micro** — set `DtrEnable = true` on the PC side.
- **Unit tests** — run the `CommandMessengerTests` project in the C# solution for systematic checks.

## Credits

| Version | Author |
|---------|--------|
| Messenger (original) | Thomas Ouellet Fredericks |
| CmdMessenger v1 | Neil Dudman |
| CmdMessenger v2 | Dreamcat4 |
| CmdMessenger v3–v4 | Thijs Elenbaas & Valeriy Kucherenko |

## License

[MIT](LICENSE.md) — Copyright © 2013–2026 Thijs Elenbaas.

<details>
<summary>Changelog</summary>

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
* [.Net/.Mono] Full threading redesign
* [.Net/.Mono] AutoConnect & watchdog functionality
* [.Net/.Mono] Tested Linux compatibility
* [.Net/.Mono] Visual Basic samples
* [.Net/.Mono] Native Bluetooth support (Windows only)

### v3.6
* [Arduino] Bugfix: ~1 in 1000 commands failed with multiple binary parameters
* [Arduino] Bugfix: binary send of non-number compile error
* [Arduino] Feature: send command without argument
* [Arduino] Feature: scientific notation for full float range
* [.Net/.Mono] Unit tests, performance improvements

### v3.5
* [Arduino] Console shell sample, minor performance improvement

### v3.4
* [Arduino] Bug-fix in receiving binary values
* [.Net/.Mono] 100× faster communication, single-core support

### v3.3
* [Arduino] Speed improvements for Teensy

### v3.2
* [All] Clean transport layer interface (Bluetooth, ZigBee, Web)
* [.Net/.Mono] Adaptive throttling, smart queuing

### v3.0
* Multi-argument commands, type arguments, binary data, escaping, acknowledgements

### v2
* Arduino IDE 022, configurable separators, Base-64 encoding, Serial Monitor debugging

</details>

