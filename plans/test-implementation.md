# Test Implementation Plan

Status: **In Progress**  
Last updated: 2026-06-01

---

## Overview

Three test layers for CmdMessenger, from fast/cheap to slow/realistic:

| Layer | Scope | Runtime | CI? |
|-------|-------|---------|-----|
| 1 | C# host unit tests | ~2 sec | Yes |
| 2 | Firmware unit tests (native) | ~5 sec | Yes |
| 3a | Cross-stack loopback (no HW) | ~10 sec | Yes |
| 3b | Hardware-in-the-loop | ~30 sec | Opt-in |

---

## Layer 1 — Host-side unit tests (C#)

**Location:** `test/CSharp/CommandMessenger.Tests/`  
**Framework:** xUnit + net8.0 (test project only; main library stays net40)  
**Build flag:** `--unit-csharp`

### Tasks

- [x] 1.1 Create xUnit test project targeting net8.0
- [x] 1.2 Add project reference to `CommandMessenger.csproj` (multi-target if needed)
- [x] 1.3 Implement `LoopbackTransport : ITransport` (in-memory two-queue stub)
- [x] 1.4 Test: field encoding/decoding — all types, plain text mode
- [x] 1.5 Test: field encoding/decoding — all types, binary mode
- [x] 1.6 Test: escape/unescape round-trip (separator, field-sep, escape char, newline inside payload)
- [x] 1.7 Test: command framing edge cases (empty, max-length, trailing whitespace, CRLF vs LF)
- [x] 1.8 Test: receive callback dispatch (by ID, default handler, unknown, malformed)
- [x] 1.9 Test: send with ACK, timeout, retry
- [x] 1.10 Test: queue/threading under synthetic load
- [x] 1.11 Wire into `build.csx` as `--unit-csharp` step
- [x] 1.12 Green run, commit

### Notes

- The existing `extras/CSharp/CommandMessengerTests/` has skeleton code — evaluate whether to reuse or start fresh under `test/`.
- Must reference the CommandMessenger library source. If net40→net8.0 multi-target is painful, create a thin netstandard2.0 shim or copy sources into the test project.

---

## Layer 2 — Embedded-side unit tests (firmware, native)

**Location:** `test/embedded/`  
**Framework:** PlatformIO Unity, `platform = native`  
**Build flag:** `--unit-firmware`

### Tasks

- [x] 2.1 Create `test/embedded/platformio.ini` with `[env:native]`
- [x] 2.2 Write minimal `Stream`/`Print`/`millis()` shim for native builds
- [x] 2.3 Test: `feedinSerialData()` byte-at-a-time, chunked, all-at-once
- [x] 2.4 Test: escape/unescape symmetry (same vectors as Layer 1)
- [x] 2.5 Test: `readInt16Arg` / `readFloatArg` / `readStringArg` boundaries
- [x] 2.6 Test: buffer overflow guards
- [x] 2.7 Test: callback attach/detach/dispatch/default handler
- [x] 2.8 Wire into `build.csx` as `--unit-firmware` step (`pio test -e native`)
- [x] 2.9 Green run, commit

### Notes

- `CmdMessenger.cpp` uses `Stream`, `Print`, `millis()`. The shim only needs those APIs — no actual hardware.
- ArduinoCore-API stub (arduino/ArduinoCore-API on GitHub) is an option but may be overkill. Prefer a hand-rolled 50-line shim.

---

## Layer 3a — Cross-stack loopback integration (no hardware)

**Location:** `test/integration/`  
**Framework:** xUnit (C# driver) + firmware compiled to native executable  
**Build flag:** `--integration-loopback`

### Tasks

- [x] 3a.1 Define shared scenario format (YAML or JSON) under `test/scenarios/`  
  *Decision: scenarios are xUnit `[Theory]` data; no separate DSL/file format needed.*
- [x] 3a.2 Build firmware `native` variant that reads/writes stdin/stdout (pipe-friendly)
- [x] 3a.3 C# test harness: spawn firmware-native process, connect via redirected streams
- [x] 3a.4 Implement scenario runner in C# (xUnit drives `FirmwareProcessTransport` via real `CmdMessenger`)
- [x] 3a.5 Write scenarios: basic echo, type round-trips, ACK flow, error handling
- [x] 3a.6 Wire into `build.csx` as `--skip-integration` (off-by-default = on by default)
- [x] 3a.7 Green run, commit

### Notes

- On Windows, redirected stdin/stdout pipes work natively. No `com0com` or `socat` needed for this layer.
- The firmware-native build reuses the Stream shim from Layer 2, wired to stdin/stdout.

---

## Layer 3b — Hardware-in-the-loop (real board)

**Location:** `test/integration/sketch/` + `test/CSharp/CommandMessenger.IntegrationTests/HardwareIntegrationTests.cs`  
**Framework:** xUnit (C# driver) + physical Nano  
**Build flag:** `--run-hardware` (off by default; `--skip-hardware` to be explicit)

### Tasks

- [x] 3b.1 Write `LoopbackTestRunner.ino` sketch (mirrors Layer 3a command set; PIO project under `test/integration/sketch/`)
- [x] 3b.2 C# test harness: `SerialPortTransport` (`System.IO.Ports`), port from `$env:CMDMSG_HW_PORT` or first available `SerialPort.GetPortNames()` entry
- [x] 3b.3 Refactor scenarios into `LoopbackScenariosBase` so the same 33 tests run for both Layer 3a (subprocess) and 3b (serial)
- [x] 3b.4 Wire into `build.csx` as `--run-hardware` (opt-in, `[Trait("Category","Hardware")]` filter)
- [x] 3b.5 Green run on COM11 Nano (33/33 passing)

### Notes

- Marked `[Trait("Category", "Hardware")]` so default `dotnet test` runs only Layer 3a; build script uses `--filter` to separate the two.
- Sketch must be flashed manually: `cd test/integration/sketch; pio run -e nano --target upload` (no `arduino-cli` dependency).
- AVR `double = float` (4 B). `Print::print(double, 8)` overflows its internal buffer for large magnitudes (e.g. 1.5e10); sketch uses `sendCmdSciArg(val, 7)` (scientific notation) which preserves full float precision and stays well within the buffer.
- Nano CH340 resets when DTR is asserted on serial open; `BootTimeoutMs` raised to 8 s in the hardware fixture to cover the ~1.5 s bootloader wait plus margin.
- Stale `~/OneDrive/.../Arduino/libraries/CmdMessenger` on dev machine — delete before running if PIO complains about library conflicts.

---

## Shared infrastructure

| Item | Status |
|------|--------|
| Scenario format schema | Not started |
| `build.csx` new flags wiring | Not started |
| `build.bat` already forwards args | Done |

---

## Implementation order

1. **Layer 1** (highest value per hour)
2. **Shared scenario format**
3. **Layer 2**
4. **Layer 3a**
5. **Layer 3b** (last — most plumbing, lowest marginal ROI once 3a is green)

---

## Decisions to make

| Question | Decision | Date |
|----------|----------|------|
| xUnit vs NUnit for C# tests? | **xUnit** | 2026-06-01 |
| net8.0 test project or netstandard2.0 shim? | net8.0 test project | 2026-06-01 |
| Scenario format: YAML or JSON? | **JSON** | 2026-06-01 |
| Reuse `extras/CSharp/CommandMessengerTests` or fresh `test/CSharp/`? | **Fresh `test/CSharp/`** | 2026-06-01 |

---

## Progress log

| Date | What |
|------|------|
| 2026-06-01 | Plan written |
| 2026-06-01 | Layer 1 complete: 62 xUnit tests passing (EscapingTests, CommandTests, CmdMessengerTests) |
| 2026-06-01 | Layer 1 edge cases added: 98 xUnit tests. Found + fixed C# `Unescape` trailing-escape IndexOutOfRangeException |
| 2026-06-01 | Layer 2 complete: 86 PlatformIO Unity tests across 6 suites. Found + fixed buffer-overflow dispatch bug in `processLine()` |
| 2026-06-01 | Layer 3a complete: 33 xUnit tests driving a native loopback firmware via stdin/stdout pipes. Verified bidirectional escape/unescape symmetry (caller-managed in both stacks) |
| 2026-06-01 | Layer 3b complete: 33 xUnit tests running same scenarios against a real Arduino Nano over serial (COM11 / CH340). Scenarios refactored into `LoopbackScenariosBase`; `--run-hardware` opt-in flag added to `build.csx`. Found AVR `Print::print(double, 8)` buffer overflow for large magnitudes — switched sketch to `sendCmdSciArg` |
