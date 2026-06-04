# Prompt: TypeScript Port of CmdMessenger

## What this is

A kickoff prompt for a new session that will implement a TypeScript port of the CmdMessenger host-side library. Hand this file to the new thread as context.

---

## Repository & existing ports

This repo (`Arduino-CmdMessenger`) contains:

- **Arduino firmware** (`src/`): the embedded C++ library
- **C# host** (`extras/CSharp/`): `CommandMessenger/`, `Transport/`, `Samples/`, `Tests/`
- **Python host** (`extras/Python/`): `cmd_messenger/`, `samples/`, `tests/`
- **VB wrapper** (`extras/VisualBasic/`): thin layer over C# libs

The TypeScript port goes in `extras/TypeScript/` following the same structure.

---

## Protocol (wire format)

| Element | Default | Notes |
|---------|---------|-------|
| Command separator | `;` | Terminates a message |
| Field separator | `,` | Between cmd ID and args |
| Escape char | `/` | Prefixes literal `,` `;` `/` in payload |
| Line ending | `\r\n` | Optional, after `;` when `printLfCr=true` |

Text message: `<cmdId>,<arg1>,<arg2>,...;\r\n`

Binary mode: values serialized byte-by-byte, each byte individually escaped. `BoardType` (16-bit AVR vs 32-bit ARM) controls int/float widths.

Arduino constraints: 64-byte cmd buffer, 512-byte stream buffer, 5s ACK timeout, max 50 callbacks.

---

## Decisions already made

1. **Single package, all transports included.** No monorepo/multi-package split. One `npm install cmd-messenger` gives you serial, TCP, WebSerial, WebSocket. Transport backends (e.g. `serialport`) are peer/optional deps.

2. **Async pump architecture (match Python/C#).** A single sequential processing loop ("pump") that:
   - Receives incoming bytes → parses → dispatches callbacks
   - Accepts outgoing commands posted from any context
   - Processes send queue sequentially (enables message collapse strategies)
   - Callers `await sendCommand(cmd)` which posts to the pump and resolves when ACK arrives (or times out)
   - This mirrors the Python `AsyncWorker` / C# `SendCommandQueue` pattern

3. **Layer-by-layer implementation with smoke tests.** Build bottom-up, verify each layer works before moving to the next. Suggested layers:
   - L1: Escaping + binary converter (pure functions, zero deps)
   - L2: Command model (SendCommand, ReceivedCommand, field parsing)
   - L3: Transport interface + LoopbackTransport (for testing)
   - L4: Async pump + send/receive queues + collapse strategies
   - L5: CmdMessenger facade (attach, send, sendSync, ACK handling)
   - L6: SerialTransport (Node.js `serialport`) + TcpTransport
   - L7: WebSerial + WebSocket transports
   - L8: ConnectionManager (watchdog, auto-reconnect, device scan)

---

## Reference: Python architecture to mirror

```
cmd_messenger/
  __init__.py          ← public API barrel
  escaping.py          ← escape/unescape
  binary_converter.py  ← int16/32, float, bool encode/decode
  command.py           ← SendCommand, ReceivedCommand
  cmd_messenger.py     ← facade: attach, send, sendSync, pump
  async_worker.py      ← sequential processing loop
  queue/
    command_queue.py   ← ListQueue, SendCommandQueue, ReceiveCommandQueue
    strategies.py      ← CollapseCommandStrategy, TopCommandStrategy, StaleGeneralStrategy
  transport/
    transport.py       ← Transport ABC (connect, disconnect, read, write, onData)
    serial/            ← SerialTransport, SerialSettings
    network/           ← TcpTransport, TcpConnectionManager
  connection_manager.py ← watchdog, scan, reconnect state machine
```

Key patterns:
- `Transport.onData` fires when bytes arrive; the pump consumes them
- Send queue is processed sequentially; strategies can collapse/deduplicate pending commands
- `sendCommandSync()` posts to pump, creates a one-shot waiter for the ACK cmd ID, resolves/rejects on response or timeout
- Connection manager runs a state machine: WAIT → SCAN → CONNECT → WATCHDOG

---

## What to produce

- `extras/TypeScript/` with working `package.json`, `tsconfig.json`, `vitest` setup
- Implementation layer by layer (L1–L8), each with a smoke test verifying it works
- Final integration: connect to Arduino running `SendAndReceive` example
- Tests using `LoopbackTransport` (same pattern as C#/Python Layer 1 tests)

---

## Constraints

- TypeScript strict mode, no `any` in public API
- ESM output (`"type": "module"`)
- Node 18+ minimum
- `serialport` as optional peer dep for Node serial
- Zero runtime deps in core (escaping, commands, binary, pump)
- Naming: camelCase (JS convention), but class/concept names match Python/C# (e.g. `CmdMessenger`, `SendCommand`, `ReceivedCommand`, `Transport`)
