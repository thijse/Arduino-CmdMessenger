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

- [ ] 1.1 Create xUnit test project targeting net8.0
- [ ] 1.2 Add project reference to `CommandMessenger.csproj` (multi-target if needed)
- [ ] 1.3 Implement `LoopbackTransport : ITransport` (in-memory two-queue stub)
- [ ] 1.4 Test: field encoding/decoding — all types, plain text mode
- [ ] 1.5 Test: field encoding/decoding — all types, binary mode
- [ ] 1.6 Test: escape/unescape round-trip (separator, field-sep, escape char, newline inside payload)
- [ ] 1.7 Test: command framing edge cases (empty, max-length, trailing whitespace, CRLF vs LF)
- [ ] 1.8 Test: receive callback dispatch (by ID, default handler, unknown, malformed)
- [ ] 1.9 Test: send with ACK, timeout, retry
- [ ] 1.10 Test: queue/threading under synthetic load
- [ ] 1.11 Wire into `build.csx` as `--unit-csharp` step
- [ ] 1.12 Green run, commit

### Notes

- The existing `extras/CSharp/CommandMessengerTests/` has skeleton code — evaluate whether to reuse or start fresh under `test/`.
- Must reference the CommandMessenger library source. If net40→net8.0 multi-target is painful, create a thin netstandard2.0 shim or copy sources into the test project.

---

## Layer 2 — Embedded-side unit tests (firmware, native)

**Location:** `test/embedded/`  
**Framework:** PlatformIO Unity, `platform = native`  
**Build flag:** `--unit-firmware`

### Tasks

- [ ] 2.1 Create `test/embedded/platformio.ini` with `[env:native]`
- [ ] 2.2 Write minimal `Stream`/`Print`/`millis()` shim for native builds
- [ ] 2.3 Test: `feedinSerialData()` byte-at-a-time, chunked, all-at-once
- [ ] 2.4 Test: escape/unescape symmetry (same vectors as Layer 1)
- [ ] 2.5 Test: `readInt16Arg` / `readFloatArg` / `readStringArg` boundaries
- [ ] 2.6 Test: buffer overflow guards
- [ ] 2.7 Test: callback attach/detach/dispatch/default handler
- [ ] 2.8 Wire into `build.csx` as `--unit-firmware` step (`pio test -e native`)
- [ ] 2.9 Green run, commit

### Notes

- `CmdMessenger.cpp` uses `Stream`, `Print`, `millis()`. The shim only needs those APIs — no actual hardware.
- ArduinoCore-API stub (arduino/ArduinoCore-API on GitHub) is an option but may be overkill. Prefer a hand-rolled 50-line shim.

---

## Layer 3a — Cross-stack loopback integration (no hardware)

**Location:** `test/integration/`  
**Framework:** xUnit (C# driver) + firmware compiled to native executable  
**Build flag:** `--integration-loopback`

### Tasks

- [ ] 3a.1 Define shared scenario format (YAML or JSON) under `test/scenarios/`
- [ ] 3a.2 Build firmware `native` variant that reads/writes stdin/stdout (pipe-friendly)
- [ ] 3a.3 C# test harness: spawn firmware-native process, connect via redirected streams
- [ ] 3a.4 Implement scenario runner in C# (load YAML, send commands, assert responses)
- [ ] 3a.5 Write scenarios: basic echo, type round-trips, ACK flow, error handling
- [ ] 3a.6 Wire into `build.csx` as `--integration-loopback`
- [ ] 3a.7 Green run, commit

### Notes

- On Windows, redirected stdin/stdout pipes work natively. No `com0com` or `socat` needed for this layer.
- The firmware-native build reuses the Stream shim from Layer 2, wired to stdin/stdout.

---

## Layer 3b — Hardware-in-the-loop (real board)

**Location:** `test/integration/` (same scenarios as 3a)  
**Framework:** xUnit (C# driver) + physical Nano  
**Build flag:** `--integration-hardware` (off by default)

### Tasks

- [ ] 3b.1 Write/adapt a `TestRunner.ino` sketch (echo/probe protocol)
- [ ] 3b.2 C# test harness: auto-discover Nano by VID/PID, upload, open serial port
- [ ] 3b.3 Reuse scenario runner from 3a but over real `SerialTransport`
- [ ] 3b.4 Wire into `build.csx` as `--integration-hardware`
- [ ] 3b.5 Green run (manual verification)

### Notes

- Skipped when no board detected. Must not fail CI.
- Stale `~/OneDrive/.../Arduino/libraries/CmdMessenger` on dev machine — delete before running.

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
