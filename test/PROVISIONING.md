# Board Provisioning System

## Why provisioning?

When running hardware-in-the-loop tests against multiple Arduino boards, COM port
numbers are unreliable identifiers — they change when boards are plugged into
different USB ports, hubs, or machines. A board that was `COM12` yesterday may
appear as `COM16` today.

The provisioning system solves this by burning a **persistent identity** into each
board's EEPROM. The test infrastructure can then open any serial port, ask
"who are you?", and route the correct tests to the correct board — regardless of
which COM port it happened to land on.

## How it works

### EEPROM identity layout

Each board stores a 17-byte identity block starting at EEPROM address 0:

```
Offset  Size  Field
─────────────────────────────────
  0       1   Magic byte (0xA5) — marks the board as provisioned
  1       8   Model (null-padded ASCII, e.g. "NANO\0\0\0\0")
  9       8   ID    (null-padded ASCII, e.g. "001\0\0\0\0\0")
```

If the magic byte is missing (fresh/erased EEPROM), the board reports itself as
`UNPROVISIONED`.

### CmdMessenger commands

The LoopbackTestRunner sketch exposes two identity commands:

| Command   | ID | Direction | Fields         | Description                  |
|-----------|----|-----------|----------------|------------------------------|
| kWhoAmI   | 18 | Host→Board | (none)        | Request current identity     |
| kWhoAmIResult | 19 | Board→Host | model, id | Reply with model and ID      |
| kSetId    | 20 | Host→Board | model, id     | Write new identity to EEPROM |
| kSetIdResult | 21 | Board→Host | model, id  | Confirm what was written     |

Example exchange (text protocol at 115200 baud):

```
→  18;\n
←  19,NANO,001;

→  20,ESP32S3,002;\n
←  21,ESP32S3,002;
```

### Platform-specific EEPROM handling

| Platform         | EEPROM.begin() | EEPROM.commit() | Notes                    |
|------------------|:--------------:|:---------------:|--------------------------|
| AVR (Nano, Uno)  | Not needed     | Not needed      | True EEPROM, byte-access |
| Teensy           | Not needed     | Not needed      | True EEPROM like AVR     |
| ESP8266 / ESP32  | Required (64B) | Required        | Flash-emulated EEPROM    |
| RP2040           | Required (64B) | Required        | Flash-emulated EEPROM    |

The sketch uses compile-time macros to handle this transparently:
```cpp
#if defined(ESP8266) || defined(ESP32)
  #define EEPROM_INIT()  EEPROM.begin(64)
  #define EEPROM_SAVE()  EEPROM.commit()
#elif defined(ARDUINO_ARCH_RP2040)
  #define EEPROM_INIT()  EEPROM.begin(64)
  #define EEPROM_SAVE()  EEPROM.commit()
#else
  #define EEPROM_INIT()
  #define EEPROM_SAVE()
#endif
```

## The provisioning tool

**`test/provision.csx`** is an interactive dotnet-script that automates the entire
flash-and-identity workflow:

### Prerequisites

- [.NET 8+ SDK](https://dotnet.microsoft.com/download)
- [dotnet-script](https://github.com/dotnet-script/dotnet-script) (`dotnet tool install -g dotnet-script`)
- [PlatformIO CLI](https://docs.platformio.org/en/latest/core/installation.html)

### Usage

```
dotnet script test/provision.csx
```

### Workflow

1. **Port detection** — Lists current USB-serial ports, then waits for a new one
   to appear (plug in a board).

2. **Chip identification** — Probes the new port using:
   - `esptool.py chip_id` for ESP32/ESP8266 variants
   - `avrdude -n` for AVR chips (Nano, Uno)
   - USB VID:PID matching for Teensy (VID `16C0`, PID `0483`)

3. **Firmware flash** — Runs `pio run -e <env> --target upload --upload-port <port>`
   with the correct PlatformIO environment for the detected board.

4. **Identity check** — Opens the serial port, waits for the board to boot, sends
   `kWhoAmI` to read the current identity.

5. **Assignment** — If unprovisioned, auto-generates the next sequential ID for
   that model (e.g. `NANO-001`, `NANO-002`) and writes it via `kSetId`.

6. **Registry update** — Saves the board entry to `test/provisioned.json`:
   ```json
   {
     "NANO-001": {
       "Model": "NANO",
       "Id": "001",
       "ChipInfo": "m328p",
       "LastPort": "COM13",
       "ProvisionedAt": "2026-06-01T12:00:00Z"
     }
   }
   ```

7. **Loop** — Returns to step 1 to provision the next board. Ctrl-C to quit.

### Supported boards

| Chip Signature | Model Prefix | PIO Environment |
|----------------|--------------|-----------------|
| m328p          | NANO         | nano            |
| m328 / atmega328 | UNO       | uno             |
| esp32-s3       | ESP32S3      | esp32s3         |
| esp32          | ESP32        | esp32           |
| esp8266        | ESP8266      | esp8266         |
| rp2040         | RP2040       | rp2040          |
| VID 16C0:0483  | TEENSY       | teensy30        |

## Registry file

`test/provisioned.json` is the source of truth for which boards have been
provisioned. The test runner reads this file to discover available hardware and
map identities to ports. The file is not committed to version control (each
developer's set of physical boards is different).

## Using identities in tests

The hardware test runner can enumerate COM ports, send `kWhoAmI` to each, and
build a map of identity → port. This means tests can target a specific board by
model name rather than a hard-coded COM port:

```csharp
// Find the ESP32-S3 regardless of which COM port it's on
var port = DiscoverBoard("ESP32S3", "001");
var transport = new SerialPortTransport(port, 115200);
```

This makes the test infrastructure portable across machines and USB configurations.

## Performance considerations

The hardware test suite runs 33 scenarios per board sequentially. Each test
creates a fresh serial connection and waits for the board to become ready. This
design maximizes test isolation but has performance implications:

| Board | 33 tests | Per-test | Why |
|-------|----------|----------|-----|
| ESP32-S3 | ~32s | ~1s | Native USB — no DTR reset, instant ping response |
| Nano | ~77s | ~2.3s | DTR resets MCU; AVR bootloader is fast (~1.5s boot) |
| ESP8266 | ~295s | ~9s | DTR resets MCU; WiFi stack init is genuinely slow |
| Teensy 3.0 | ~296s | ~9s | Native USB ignores DTR — 8s boot-ack timeout wasted per test, then instant ping fallback |

**Root causes of slow boards:**

- **ESP8266**: The hardware truly needs ~8-9s to boot after a DTR reset (ROM
  bootloader → flash read → WiFi calibration → `setup()`). This is inherent to
  the platform and cannot be reduced without keeping the connection open.

- **Teensy**: The MCU never resets on serial open (native USB doesn't toggle
  reset on DTR). The test waits the full `BootTimeoutMs` (8s) for a boot ack
  that will never arrive, then falls back to a ping/pong handshake that responds
  instantly. This 8s dead wait per test is the entire overhead.

**Possible optimization (not implemented — adds complexity):**

A shared-connection fixture (`IClassFixture<T>`) could keep the serial port open
for all 33 tests in a board class, reducing total time to ~33s per board. This
was rejected because it significantly complicates the inheritance model (fixture
owns connection vs. instance owns connection), breaks test isolation guarantees,
and makes the code harder to follow. The ~12 minute full run is acceptable for
a hardware-in-the-loop suite that validates real physical communication.
