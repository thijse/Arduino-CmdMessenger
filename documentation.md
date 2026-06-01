# CmdMessenger Documentation

This page collects the conceptual / example‑level documentation for CmdMessenger and links to the auto‑generated API reference.

## API reference

The full class‑level reference is generated with Doxygen and published via GitHub Pages:

- [Arduino library reference](https://thijse.github.io/Arduino-CmdMessenger/Arduino/html/class_cmd_messenger.html) — the `CmdMessenger` C++ class
- [C# / .NET reference](https://thijse.github.io/Arduino-CmdMessenger/PC/html/namespaces.html) — `CommandMessenger` namespaces and types

Local copies live under [`docs/Arduino/html/`](docs/Arduino/html/) and [`docs/PC/html/`](docs/PC/html/).

## Message format

CmdMessenger sends one *command* per line:

```
Cmd Id, param 1, [...] , param N;
```

The field separator (`,`) and command separator (`;`) are both configurable. Arguments may be plain text (human readable) or binary (efficient — strings are escaped so no Base‑64 is needed).

The library can:

- both send and receive commands
- both write and read multiple arguments
- handle all primary data types (`bool`, `int16`, `int32`, `float`, `double`, `char`, `string`, raw binary)
- attach callback functions to any received command

## Getting started with the examples

The examples are organised from simple to complex. Each Arduino sketch under [`examples/`](../../examples/) has a matching C# project under [`extras/CSharp/`](../CSharp/).

### Receive
Toggles the integrated LED on the Arduino board from the PC.

- **Arduino**: define commands, set up a serial connection, receive a command with a parameter.
- **PC**: define commands, set up a serial connection, send a command with a parameter.

### SendAndReceive
Expands *Receive*: the Arduino now sends a status back.

- **Arduino**: handle received commands that have no attached function, send a command with a parameter to the PC.
- **PC**: handle received commands that have no attached function, receive a command with a parameter from the Arduino.

### SendAndReceiveArguments
Expands *SendAndReceive*: multiple typed values are exchanged.

- **Arduino**: return multi‑type status, receive and send multiple parameters, call a function periodically.
- **PC**: send multiple parameters and wait for a response, receive multiple parameters, log sent and received data.

### SendAndReceiveBinaryArguments
Expands *SendAndReceiveArguments*: arguments are exchanged in binary form for efficiency.

- **Arduino**: send and receive binary parameters.
- **PC**: receive and send multiple binary parameters, handle callbacks while the main program is busy, measure milliseconds the way `millis()` does on Arduino.

### SendWithoutSeparator
Demonstrates the `sendArg()` / `sendSciArg()` / `sendBinArg()` family — sending raw argument bytes without the field separator, for custom packet formats or interop with non‑CmdMessenger receivers.

### DataLogging
Expands *SendAndReceiveArguments*: the PC tells the Arduino to start, then plots the streamed analog data in a chart.

- Use CmdMessenger inside a GUI application.
- Use CmdMessenger together with ZedGraph.
- Use the `StaleGeneralStrategy`.

### ArduinoController
Expands *SendAndReceiveArguments*: trackbar movement on the PC queues commands to set the blink speed of the on‑board LED.

- Use CmdMessenger inside a GUI application.
- Send queued commands.
- Use the `CollapseCommandStrategy`.

### SimpleWatchdog
Communication over a virtual serial port, with auto‑recovery.

- Auto scanning and connecting.
- Watchdog.

### SimpleBluetooth
Same as *SimpleWatchdog* but over Bluetooth, tested with the JY‑MCU HC‑05 and HC‑06 modules. The Arduino side reuses `SimpleWatchdog.ino`.

- Bluetooth transport.
- Auto scanning and connecting.
- Watchdog.

### TemperatureControl
Expands *ArduinoController*: the Arduino streams temperature and heater values which the PC plots live. A slider sets the goal temperature; the PID loop on the Arduino adjusts the heater accordingly.

- Design a responsive UI that sends and receives commands.
- Send queued commands and apply queue strategies.

### ConsoleShell
Uses CmdMessenger as a shell driven from the Arduino Serial Monitor — no PC counterpart. It only receives commands and replies with `Serial.print`.

Example session:

```
   Available commands
   0;                  - This command list
   1,<led state>;      - Set led. 0 = off, 1 = on
   2,<led brightness>; - Set led brightness. 0 - 1000
   3;                  - Show led state

 Command> 3;

  Led status: on
  Led brightness: 500

 Command> 2,1000;

   Led status: on
   Led brightness: 1000

 Command> 1,0;

   Led status: off
   Led brightness: 1000
```

### Running a paired example

1. Open the example `.ino` in the Arduino IDE, compile and upload to your board.
2. Open `extras/CSharp/CmdMessenger.sln` in Visual Studio (or MonoDevelop / Xamarin Studio).
3. Set the example project with the same name as the sketch as the start‑up project and run.

## Troubleshooting

- **Cannot connect** — check that the PC is using the right serial port and that PC and Arduino are at the same baud rate. The easiest sanity check is to flash *ConsoleShell* and type commands from the Arduino Serial Monitor.
- **Sparkfun Pro Micro and similar boards** — set `DtrEnable = true` on the PC side.
- **Callbacks are not invoked** — enable logging of sent and received data; see *SendAndReceiveArguments*.
- **Hard‑to‑pinpoint issue** — run the `CommandMessengerTests` project in the C# solution. Note that it is a test suite, not a tutorial, so the code is less heavily commented than the examples.

## Notes

A Max5 / MaxMSP example existed up to version 2; it has been removed because it is no longer being tested. The historical version is preserved in [dreamcat4's fork](https://github.com/dreamcat4/CmdMessenger).
