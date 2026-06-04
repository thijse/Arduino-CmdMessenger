# TypeScript CmdMessenger Library — Architecture Plan

## 0. Decisions

| # | Decision | Choice |
|---|---------|--------|
| 1a | npm package name | `cmd-messenger` |
| 1b | Import name | `cmd-messenger` (barrel `index.ts`) |
| 1c | Node.js version floor | 22+ |
| 1d | Module system | ESM (`"type": "module"`) |
| 1e | Repo location | `extras/TypeScript/` (self-contained project) |
| 2 | Concurrency model | **Async/await, single event loop** — see §0.1 |
| 3 | Event API | Typed `EventEmitter` pattern (see §0.2) |
| 4a | `bool` encoding in protocol | `"1"`/`"0"` — mirror C#/Python exactly |
| 4b | String encoding | ISO-8859-1 (latin-1) via custom `string` ↔ `Uint8Array` helpers; no `Buffer`/`TextEncoder` in core |
| 4c | Binary endianness | Little-endian explicit (`DataView`) |
| 4d | Double on Bit16 boards | Encoded/decoded as float32 — mirror C#/Python |
| 5 | Tests | Vitest — layered, run with `npm test` |
| 6 | Documentation | TSDoc comments on public API |
| 7a | `SendCommand` argument API | Varargs + options object + fluent `.withAck()` / `.addArgument()` |
| 7b | `ReceivedCommand` reader API | Iterator-style `readInt32Arg()` etc. + optional `read("ifs")` format-string |
| 7c | Connect-failure behaviour | Throws `Error`; `isConnected()` for status |
| 7d | Text argument escaping | Do **not** auto-escape text args; callers explicitly use `escape(...)`, matching C#/Python behavior |
| 7e | Binary argument widths | Provide explicit width-specific methods (`addBinInt16Argument`, `addBinUint32Argument`, `addBinFloatArgument`, etc.) because TS `number` has no overload-level width |
| 7f | Porting baseline | C# is the behavioral reference; TypeScript deviates only for language/runtime conventions or documented additive extensions |
| 8a | License | MIT |
| 8b | Build tool | `tsc` → ESM output |
| 8c | Strict mode | `strict: true`, no `any` in public API |
| 9 | Initial platform scope | **Node-first v1**; browser transports deferred until the core + Node transports are stable |

### 0.0 Progress Log

| Date | Progress |
|------|----------|
| 2026-06-03 | Initial `extras/TypeScript/` package scaffolded (`package.json`, `tsconfig`, Vitest, README), Node floor raised to 22+, core runtime kept dependency-free. |
| 2026-06-03 | Implemented L1-L5 initial slice: escaping, latin-1 byte codec, binary converter, command models, received readers, promise ACK waiter, send/receive queues, `CommunicationManager`, `CmdMessenger`, and `LoopbackTransport`. |
| 2026-06-03 | Added 18 Vitest tests covering escaping, binary conversion, command serialization, no-auto-escape text behavior, callback dispatch, ACK success, and ACK timeout. |
| 2026-06-03 | Wired TypeScript into `test/build.csx` with `--skip-typescript`; verified `npm ci`, `npm run build`, and `npm test` through the repo build script. |
| 2026-06-03 | Implemented L6 Node transports: `TcpTransport`, optional-peer `SerialTransport`, `SerialSettings`, and serial utilities. Added TCP tests with an in-process `node:net` server and serial settings tests; suite now 23 tests. |
| 2026-06-03 | Implemented Node-first L8 connection managers: async `ConnectionManager`, `TcpConnectionManager`, and `SerialConnectionManager` scan/connect/watchdog scaffolding. Added TCP connection-manager tests; suite now 25 tests. |
| 2026-06-03 | Verified `npm run typecheck`, `npm run build`, `npm test`, `npm audit`, and repo build-script TypeScript path. All pass; npm audit reports 0 vulnerabilities. |
| 2026-06-03 | Added `serialport` as a dev dependency while retaining it as an optional peer dependency for consumers. Added `rtsEnable` next to `dtrEnable` and bounded serial open/close with `SerialSettings.timeout` so bad ports cannot hang discovery. |
| 2026-06-03 | Added transport-agnostic TypeScript loopback scenario helpers plus an in-process firmware simulator mirroring the C#/Python loopback scenario contract. Normal Vitest suite now covers 36 tests. |
| 2026-06-03 | Added opt-in TypeScript hardware suite (`npm run test:hardware`) using `SerialTransport`, USB serial discovery, `CMDMSG_HW_PORT`/`CMDMESSENGER_PORT` overrides, boot ACK with ping fallback, and the shared loopback scenarios. Verified against connected hardware. |
| 2026-06-03 | Wired TypeScript hardware tests into `test/build.csx` under `--run-hardware` while keeping regular TypeScript build/test hardware-free. Verified clean `npm ci`, build, tests, typechecks, audit, and hardware suite. |
| 2026-06-04 | Recorded C#-first porting rule: keep TypeScript behavior aligned with C# unless TypeScript/Node conventions require a different expression; C# design concerns go into `plans/architecture-comparison.md` as reference-code suggestions. |

### 0.1 Concurrency model rationale

**Choice: native async/await on the Node.js event loop for v1.**

| Aspect | Async/await (chosen) | Worker threads (mirror C#) |
|--------|---------------------|---------------------------|
| Matches JS/TS idiom | ✅ natural | ❌ unidiomatic, complex |
| Matches C# architecture | Conceptually — `SendCommandQueue` becomes an async drain loop | Literally — but wrong for JS |
| `await sendCommand(cmd)` | ✅ native | ✅ but with message passing overhead |
| Queue + strategies | Sequential `async` loop processes one at a time | Same, but on a Worker |
| Transport integration | `serialport` fires events on main loop already | Must marshal between threads |
| Browser compatibility | Deferred until after core + Node transports | ❌ (no Worker threads for serial) |

The C# `AsyncWorker` (background thread + signal) maps to an **async generator / drain loop** in TypeScript:
```typescript
// Conceptual equivalent of AsyncWorker
private async processSendQueue(): Promise<void> {
  while (this._running) {
    const strategy = await this._sendQueue.dequeue(); // awaits signal
    await this._executeSend(strategy.command);
  }
}
```

This preserves the same sequential, single-consumer semantics without threads.

### 0.2 Event system

TypeScript uses a typed event emitter pattern:

```typescript
type MessengerCallback = (cmd: ReceivedCommand) => void;

interface CmdMessengerEvents {
  newLineReceived: (cmd: ReceivedCommand) => void;
  newLineSent: (cmd: SendCommand) => void;
}

// Internal: lightweight typed emitter (no external dep)
class TypedEmitter<T extends { [K in keyof T]: (...args: never[]) => void }> {
  on<K extends keyof T>(event: K, handler: T[K]): this;
  off<K extends keyof T>(event: K, handler: T[K]): this;
  emit<K extends keyof T>(event: K, ...args: Parameters<T[K]>): void;
}
```

This maps to:
- C# `event EventHandler<CommandEventArgs>` → TypeScript `.on('newLineReceived', handler)`
- Python `Event` with `+=`/`-=` → TypeScript `.on()` / `.off()`

---

## 1. Overview

A Node-first TypeScript port of the C#/Python `CommandMessenger` library. Same class hierarchy, same separation of concerns, adapted to async/await idioms. Browser transports remain planned follow-up work after the core and Node transports are stable.

**npm package:** `cmd-messenger`  
**Import:** `import { CmdMessenger, SendCommand, ReceivedCommand } from 'cmd-messenger'`

---

## 2. Package Layout

```
extras/TypeScript/
├── package.json
├── tsconfig.json
├── vitest.config.ts
├── src/
│   ├── index.ts                        # Public API barrel
│   ├── cmdMessenger.ts                 # CmdMessenger (main façade)
│   ├── command.ts                      # Command base class
│   ├── sendCommand.ts                  # SendCommand
│   ├── receivedCommand.ts             # ReceivedCommand
│   ├── communicationManager.ts        # CommunicationManager
│   ├── connectionManager.ts           # ConnectionManager (abstract)
│   ├── binaryConverter.ts             # BinaryConverter
│   ├── escaping.ts                    # Escaping + isEscaped
│   ├── eventWaiter.ts                 # EventWaiter (Promise-based signal)
│   ├── receivedCommandSignal.ts       # ReceivedCommandSignal
│   ├── timeUtils.ts                   # TimeUtils
│   ├── logger.ts                      # Logger
│   ├── enums.ts                       # SendQueue, ReceiveQueue, UseQueue, BoardType
│   ├── typedEmitter.ts                # Lightweight typed event emitter
│   ├── queue/
│   │   ├── index.ts
│   │   ├── listQueue.ts              # ListQueue<T>
│   │   ├── commandQueue.ts           # CommandQueue (abstract)
│   │   ├── sendCommandQueue.ts       # SendCommandQueue
│   │   ├── receiveCommandQueue.ts    # ReceiveCommandQueue
│   │   ├── commandStrategy.ts        # CommandStrategy
│   │   ├── collapseCommandStrategy.ts # CollapseCommandStrategy
│   │   ├── topCommandStrategy.ts     # TopCommandStrategy
│   │   ├── generalStrategy.ts        # GeneralStrategy
│   │   └── staleGeneralStrategy.ts   # StaleGeneralStrategy
│   └── transport/
│       ├── index.ts
│       ├── transport.ts               # ITransport interface
│       ├── loopbackTransport.ts       # LoopbackTransport (testing)
│       ├── serial/
│       │   ├── index.ts
│       │   ├── serialTransport.ts     # SerialTransport (Node serialport)
│       │   ├── serialSettings.ts      # SerialSettings
│       │   ├── serialConnectionManager.ts
│       │   └── serialUtils.ts
│       ├── network/
│       │   ├── index.ts
│       │   ├── tcpTransport.ts        # TcpTransport (Node net)
│       │   └── tcpConnectionManager.ts
│       ├── webserial/
│       │   ├── index.ts
│       │   └── webSerialTransport.ts  # WebSerialTransport (browser)
│       └── websocket/
│           ├── index.ts
│           └── webSocketTransport.ts  # WebSocketTransport (browser/Node)
└── tests/
    ├── escaping.test.ts
    ├── binaryConverter.test.ts
    ├── command.test.ts
    ├── sendCommand.test.ts
    ├── receivedCommand.test.ts
    ├── communicationManager.test.ts
    ├── queue.test.ts
    ├── strategies.test.ts
    ├── cmdMessenger.test.ts
    ├── loopback.test.ts
    └── integration.test.ts
```

---

## 3. Class & Interface Design

### 3.1 Enums — `enums.ts`

```typescript
export enum SendQueue {
  Default = 0,
  InFrontQueue = 1,
  AtEndQueue = 2,
  WaitForEmptyQueue = 3,
  ClearQueue = 4,
}

export enum ReceiveQueue {
  Default = 0,
  WaitForEmptyQueue = 1,
  ClearQueue = 2,
}

export enum UseQueue {
  UseQueue = 0,
  BypassQueue = 1,
}

export enum BoardType {
  Bit16 = 0,
  Bit32 = 1,
}
```

### 3.2 `ITransport` — `transport/transport.ts`

```typescript
export interface ITransport {
  readonly dataReceived: TypedEmitter<{ data: () => void }>;

  connect(): Promise<boolean>;
  disconnect(): Promise<boolean>;
  isConnected(): boolean;
  read(): Uint8Array;
  write(data: Uint8Array): Promise<void>;
  dispose(): void;
}
```

### 3.3 `Command` — `command.ts`

```typescript
export class Command {
  cmdId: number;
  arguments: string[];
  timeStamp: number;

  get ok(): boolean;
  commandString(): string;
}
```

### 3.4 `SendCommand` — `sendCommand.ts`

```typescript
export class SendCommand extends Command {
  reqAc: boolean;
  ackCmdId: number;
  timeout: number;

  constructor(cmdId: number, ...args: Array<string | number | boolean>);
  constructor(cmdId: number, options: { args?: Array<string | number | boolean>; ackCmdId?: number; timeout?: number });

  addArgument(value: string | number | boolean): this;
  addArguments(values: Array<string | number | boolean>): this;
  withAck(ackCmdId: number, timeout?: number): this;
  addBinStringArgument(value: string): this;
  addBinBoolArgument(value: boolean): this;
  addBinByteArgument(value: number): this;
  addBinInt16Argument(value: number): this;
  addBinUint16Argument(value: number): this;
  addBinInt32Argument(value: number): this;
  addBinUint32Argument(value: number): this;
  addBinFloatArgument(value: number): this;
  addBinDoubleArgument(value: number): this;
  addBinArgument(value: string | number | boolean): this; // convenience; explicit methods preferred for numeric widths
}
```

### 3.5 `ReceivedCommand` — `receivedCommand.ts`

```typescript
export class ReceivedCommand extends Command {
  rawString: string;

  // Iterator
  next(): boolean;
  available(): boolean;
  [Symbol.iterator](): Iterator<string>;

  // Text-mode typed readers (advance cursor)
  readInt16Arg(): number;
  readUint16Arg(): number;
  readInt32Arg(): number;
  readUint32Arg(): number;
  readFloatArg(): number;
  readDoubleArg(): number;
  readStringArg(): string;
  readBoolArg(): boolean;
  readCharArg(): string;

  // Binary-mode readers
  readBinInt16Arg(): number;
  readBinUint16Arg(): number;
  readBinInt32Arg(): number;
  readBinUint32Arg(): number;
  readBinFloatArg(): number;
  readBinDoubleArg(): number;
  readBinByteArg(): number;
  readBinStringArg(): string;
  readBinBoolArg(): boolean;

  // Optional format-string reader (Python-style)
  read(fmt: string): unknown[];
}
```

### 3.6 `CommunicationManager` — `communicationManager.ts`

```typescript
export class CommunicationManager {
  fieldSeparator: string;
  commandSeparator: string;
  escapeCharacter: string;
  boardType: BoardType;
  printLfCr: boolean;
  lastLineTimeStamp: number;

  constructor(
    transport: ITransport,
    receiveCommandQueue: ReceiveCommandQueue,
    boardType: BoardType,
    commandSeparator: string,
    fieldSeparator: string,
    escapeCharacter: string
  );

  connect(): Promise<boolean>;
  disconnect(): Promise<boolean>;
  write(value: string): Promise<void>;
  writeLine(value: string): Promise<void>;
  executeSendCommand(sendCommand: SendCommand, sendQueueState: SendQueue): Promise<ReceivedCommand>;
  executeSendString(commandString: string, sendQueueState: SendQueue): Promise<ReceivedCommand>;
  dispose(): void;
}
```

### 3.7 `CmdMessenger` — `cmdMessenger.ts`

```typescript
export type MessengerCallback = (cmd: ReceivedCommand) => void;

export class CmdMessenger {
  printLfCr: boolean;
  readonly lastReceivedCommandTimeStamp: number;

  // Events
  readonly events: TypedEmitter<{
    newLineReceived: (cmd: ReceivedCommand) => void;
    newLineSent: (cmd: SendCommand) => void;
  }>;

  constructor(
    transport: ITransport,
    boardType?: BoardType,
    fieldSeparator?: string,
    commandSeparator?: string,
    escapeCharacter?: string,
    sendBufferMaxLength?: number
  );

  connect(): Promise<boolean>;
  disconnect(): Promise<boolean>;

  // Callback registration (mirrors C# Attach)
  attach(callback: MessengerCallback): void;                    // default
  attach(cmdId: number, callback: MessengerCallback): void;     // per-command

  // Send
  sendCommand(
    cmd: SendCommand,
    sendQueue?: SendQueue,
    receiveQueue?: ReceiveQueue,
    useQueue?: UseQueue
  ): Promise<ReceivedCommand>;

  sendCommandSync(cmd: SendCommand, sendQueue?: SendQueue): Promise<ReceivedCommand>;

  queueCommand(cmd: SendCommand | CommandStrategy): void;

  // Strategy hooks
  addSendCommandStrategy(strategy: GeneralStrategy): void;
  addReceiveCommandStrategy(strategy: GeneralStrategy): void;

  // Queue management
  clearSendQueue(): void;
  clearReceiveQueue(): void;

  dispose(): void;
}
```

### 3.8 `ConnectionManager` — `connectionManager.ts`

```typescript
export interface ConnectionManagerEvents {
  connectionFound: () => void;
  connectionTimeout: () => void;
  progress: (percent: number, status: string) => void;
}

export abstract class ConnectionManager {
  readonly events: TypedEmitter<ConnectionManagerEvents>;

  connected: boolean;
  watchdogTimeout: number;
  watchdogRetryTimeout: number;
  watchdogTries: number;
  watchdogEnabled: boolean;
  deviceScanEnabled: boolean;

  constructor(
    cmdMessenger: CmdMessenger,
    identifyCommandId?: number,
    uniqueDeviceId?: string
  );

  startConnectionManager(): void;
  stopConnectionManager(): void;
  dispose(): void;

  // Subclass hooks
  protected abstract doWorkConnect(): Promise<void>;
  protected abstract doWorkScan(): Promise<void>;
}
```

### 3.9 Queue Classes — `queue/`

| Class | Key Members |
|-------|-------------|
| `ListQueue<T>` | `enqueue(item)`, `enqueueFront(item)`, `dequeue(): T`, `peek(): T`, `count`, `isEmpty`, `clear()` |
| `CommandQueue` (abstract) | `start()`, `stop()`, `suspend()`, `resume()`, `clear()`, `count`, `isEmpty`, `addGeneralStrategy()`, `queueCommand()` (abstract) |
| `SendCommandQueue` | `sendCommand(cmd)`, `queueCommand(strategy)`, events: `newLineSent` |
| `ReceiveCommandQueue` | `queueCommand(cmd)`, `dequeueCommand()`, `prepareForCmd(cmdId, sendQueue)`, `waitForCmd(timeout): Promise<ReceivedCommand>` |

### 3.10 Strategy Classes — `queue/`

| Class | Base | Behaviour |
|-------|------|-----------|
| `CommandStrategy` | — | Default FIFO enqueue (back), dequeue (front) |
| `TopCommandStrategy` | `CommandStrategy` | Enqueue at front (priority) |
| `CollapseCommandStrategy` | `CommandStrategy` | Replace existing command with same `cmdId` |
| `GeneralStrategy` | — | Hook base: `onEnqueue()`, `onDequeue()` called for ALL items |
| `StaleGeneralStrategy` | `GeneralStrategy` | Drops commands older than `commandTimeout` ms on dequeue |

### 3.11 Transport Implementations

#### `SerialTransport` — `transport/serial/serialTransport.ts`

```typescript
export class SerialTransport implements ITransport {
  currentSerialSettings: SerialSettings;
  constructor(settings?: SerialSettings);
  // All ITransport members
}
```

#### `SerialSettings` — `transport/serial/serialSettings.ts`

```typescript
export interface SerialSettings {
  portName: string;
  baudRate: number;
  dataBits?: 5 | 6 | 7 | 8;
  stopBits?: 1 | 1.5 | 2;
  parity?: 'none' | 'even' | 'odd';
  dtrEnable?: boolean;
  timeout?: number;
}
```

#### `TcpTransport` — `transport/network/tcpTransport.ts`

```typescript
export class TcpTransport implements ITransport {
  readonly host: string;
  readonly port: number;
  timeout: number;
  constructor(host: string, port: number);
  // All ITransport members
}
```

#### `WebSerialTransport` — `transport/webserial/webSerialTransport.ts`

```typescript
export class WebSerialTransport implements ITransport {
  constructor(options?: SerialOptions);
  requestPort(): Promise<void>;  // triggers browser permission dialog
  // All ITransport members
}
```

#### `WebSocketTransport` — `transport/websocket/webSocketTransport.ts`

```typescript
export class WebSocketTransport implements ITransport {
  constructor(url: string);
  // All ITransport members
}
```

#### `LoopbackTransport` — `transport/loopbackTransport.ts`

```typescript
export class LoopbackTransport implements ITransport {
  // Echoes writes back as reads (for testing)
  feedInput(data: Uint8Array): void;  // inject test data
  getWritten(): Uint8Array;           // inspect what was sent
}
```

#### `SerialConnectionManager` — `transport/serial/serialConnectionManager.ts`

```typescript
export class SerialConnectionManager extends ConnectionManager {
  availableSerialPorts: string[];
  deviceScanBaudRateSelection: boolean;
  constructor(
    serialTransport: SerialTransport,
    cmdMessenger: CmdMessenger,
    watchdogCommandId?: number,
    uniqueDeviceId?: string
  );
}
```

#### `TcpConnectionManager` — `transport/network/tcpConnectionManager.ts`

```typescript
export class TcpConnectionManager extends ConnectionManager {
  constructor(
    tcpTransport: TcpTransport,
    cmdMessenger: CmdMessenger,
    identifyCommandId?: number,
    uniqueDeviceId?: string
  );
}
```

### 3.12 Utilities

| Module | Export | Purpose |
|--------|--------|---------|
| `escaping.ts` | `Escaping` class | `escape(input)`, `unescape(input)`, `isEscaped(input, index)` |
| `binaryConverter.ts` | `BinaryConverter` class | Encode/decode int16/32, float, double, bool ↔ escaped binary strings |
| `eventWaiter.ts` | `EventWaiter` class | Promise-based signal: `waitOne(timeoutMs): Promise<WaitState>`, `set()`, `reset()` |
| `receivedCommandSignal.ts` | `ReceivedCommandSignal` class | Block-till-ack: arms on cmdId, resolves when matching response arrives |
| `timeUtils.ts` | `TimeUtils` class | `millis()`, `hasExpired(start, timeout)` |
| `logger.ts` | `Logger` class | Optional debug logger (console or file) |
| `typedEmitter.ts` | `TypedEmitter<T>` class | Lightweight typed event emitter (zero deps) |

---

## 4. Async Pump Architecture

```
┌───────────────────────────────────────────────────────────────┐
│                     CmdMessenger (façade)                      │
│  attach() / sendCommand() / sendCommandSync()                 │
└─────────┬──────────────────────┬──────────────────────────────┘
          │                      │
┌─────────▼──────────┐  ┌───────▼───────────────┐
│ SendCommandQueue    │  │ ReceiveCommandQueue    │
│ (async drain loop)  │  │ (async drain loop)     │
└─────────┬──────────┘  └───────┬───────────────┘
          │ drain                │ process
          ▼                      ▲
┌──────────────────────┐         │
│ CommunicationManager │─────────┘  (parseLines → queueReceivedCommand)
│  write / parse       │
└─────────┬────────────┘
          │
┌─────────▼──────────┐
│   ITransport       │  (SerialTransport / TcpTransport / WebSerialTransport)
│   dataReceived     │  → fires when bytes arrive
└────────────────────┘
```

### Send path:
1. `cmdMessenger.sendCommand(cmd)` → posts to `SendCommandQueue` (or bypasses for sync)
2. `SendCommandQueue`'s async drain loop processes one strategy at a time
3. Non-ack commands batched into buffer (up to `sendBufferMaxLength`)
4. `communicationManager.executeSendString()` writes to transport

### Receive path:
1. `ITransport.dataReceived` fires when bytes arrive
2. `CommunicationManager.parseLines()` splits on unescaped `;`, creates `ReceivedCommand`
3. `ReceiveCommandQueue.queueCommand(receivedCommand)` enqueues
4. Drain loop dequeues → `CmdMessenger.handleMessage()` → dispatches to callbacks

### Synchronous ACK flow:
1. `SendCommand` has `reqAc = true`, `ackCmdId`, `timeout`
2. `ReceiveCommandQueue` is **suspended** (stops dispatching)
3. `ReceivedCommandSignal.prepareForWait(ackCmdId)` arms the waiter
4. Command sent over wire
5. Incoming data still parsed → `ReceivedCommandSignal.processCommand()` checks match
6. If match: resolves the Promise, returns the ack command
7. If timeout: resolves with empty `ReceivedCommand` (`ok === false`)
8. Queue **resumed** (buffered commands dispatched)

### Key difference from C#/Python:
- C#/Python use **background threads** + `Monitor.Wait` / `threading.Event`
- TypeScript uses **Promises** + `async`/`await` on the single event loop
- Same logical flow, different synchronization primitives

---

## 5. Threading Model Mapping

| C# / Python | TypeScript Equivalent |
|-------------|----------------------|
| `AsyncWorker` (background thread + signal) | `async` drain loop + `Promise`-based signal |
| `threading.Lock` / `lock {}` | Not needed (single-threaded); use queue discipline |
| `threading.Event` / `Monitor.Wait` | `EventWaiter` backed by `Promise` + resolver |
| `Thread.Sleep(ms)` | `await delay(ms)` (setTimeout wrapper) |
| `SpinWait.SpinUntil(...)` | `await waitUntil(predicate, timeoutMs)` |
| Daemon thread auto-stops | AbortController / cleanup on `dispose()` |

---

## 6. Dependencies

### Runtime (zero for core)
- Core modules (escaping, commands, binary, queues, pump): **zero dependencies**
- Future `SerialTransport`: `serialport` as optional peer dep (Node.js)
- Future browser transports: browser API types only; do not leak DOM imports into core

### Dev
- `typescript` ^5.8
- `vitest` — test runner
- `tsc` — ESM build and declarations
- `@types/node` — Node.js types

### package.json peer deps:
```json
{
  "peerDependencies": {
    "serialport": ">=12.0.0"
  },
  "peerDependenciesMeta": {
    "serialport": { "optional": true }
  }
}
```

---

## 7. Node-first, Browser Later

Current implementation is Node-first. Core code remains environment-agnostic by avoiding Node-only APIs such as `Buffer`; browser transports are still deferred.

| Feature | Node.js | Browser |
|---------|---------|---------|
| Serial | `SerialTransport` (via `serialport` npm) | `WebSerialTransport` (via Web Serial API) |
| TCP | `TcpTransport` (via `net` module) | Not available |
| WebSocket | `WebSocketTransport` (via `ws` or native) | `WebSocketTransport` (native WebSocket) |
| File I/O (settings) | `JsonConnectionStorer` | `LocalStorageConnectionStorer` |
| Event loop | Node.js event loop | Browser event loop |

The core library is environment-agnostic (no Node.js or DOM APIs). Transport implementations import platform-specific APIs.

---

## 8. Implementation Layers

| Layer | What | Test Strategy |
|-------|------|---------------|
| L1 | `Escaping`, `BinaryConverter` | Done initial: pure unit tests, known vectors |
| L2 | `Command`, `SendCommand`, `ReceivedCommand` | Done initial: construct, serialize, parse, explicit binary widths |
| L3 | `ITransport`, `LoopbackTransport` | Done initial: write/read/feed test helper |
| L4 | `ListQueue`, `CommandQueue`, `SendCommandQueue`, `ReceiveCommandQueue`, strategies | Done initial: async queue primitives and ACK suspension path; broaden strategy tests next |
| L5 | `CommunicationManager`, `CmdMessenger` | Done initial: loopback callback dispatch, direct send, ACK success/timeout |
| L6 | `SerialTransport`, `TcpTransport` | Done: TCP tested with in-process server; serial optional-peer transport compiles, settings/utils are tested, and `SerialTransport` passes opt-in hardware loopback scenarios |
| L7 | `WebSerialTransport`, `WebSocketTransport` | Browser manual test + mock |
| L8 | `ConnectionManager`, `SerialConnectionManager`, `TcpConnectionManager` | Done initial: async state machine and TCP connection-manager ACK test; keep same-ID identify ACK behavior for C# parity; serial scan hardware test still needed |

### Current Next Steps

- Keep `ConnectionManager` same-ID identify ACK behavior aligned with C#. If paired identify request/response IDs are desirable, propose and implement that in the C# reference first, then port it.
- Add `SerialConnectionManager` hardware coverage using a C#-compatible same-ID identify command or a dedicated test fixture.
- Broaden queue strategy tests for collapse/top/stale behavior under queued sends.
- Defer L7 browser transports until Node serial/TCP integration is stable.

---

## 9. Example Usage (Target API)

```typescript
import { CmdMessenger, SendCommand, BoardType } from 'cmd-messenger';
import { SerialTransport } from 'cmd-messenger/transport/serial';

// Setup transport
const transport = new SerialTransport({
  portName: 'COM3',
  baudRate: 115200,
});

// Create messenger
const messenger = new CmdMessenger(transport, BoardType.Bit16);

// Attach callbacks
messenger.attach(0, (cmd) => {
  console.log(`Arduino says: ${cmd.readStringArg()}`);
});

// Connect and send
await messenger.connect();

const response = await messenger.sendCommand(
  new SendCommand(0).withAck(1, 1000)
);

if (response.ok) {
  console.log(response.readStringArg());
}

// Cleanup
await messenger.disconnect();
messenger.dispose();
```

---

## 10. Naming Convention Translation

| C# | Python | TypeScript |
|----|--------|-----------|
| `ClassName` | `ClassName` | `ClassName` |
| `MethodName()` | `method_name()` | `methodName()` |
| `PropertyName` | `property_name` | `propertyName` |
| `_privateField` | `_private_field` | `_privateField` / `#privateField` |
| `CONSTANT` | `CONSTANT` | `CONSTANT` |
| `IInterface` | `Transport` (ABC) | `ITransport` (interface) |
| `event EventHandler<T>` | `Event` with `+=`/`-=` | `TypedEmitter.on()`/`.off()` |
| `delegate void Func(T)` | `Callable[[T], None]` | `(arg: T) => void` |

---

## 11. Deviations from C# Architecture

| # | C# Design | TypeScript Adaptation | Reason |
|---|-----------|----------------------|--------|
| 1 | PascalCase methods | camelCase | JavaScript convention |
| 2 | Background threads (`AsyncWorker`) | Async drain loops (Promises) | JS is single-threaded |
| 3 | `lock {}` / `Monitor` | Unnecessary — single event loop | No shared-memory concurrency |
| 4 | `IDisposable` / `using` | `dispose()` method + optional `using` (TC39 Explicit Resource Management) | JS equivalent |
| 5 | `System.IO.Ports.SerialPort` | `serialport` npm package | Platform library |
| 6 | `TcpClient` | Node.js `net.Socket` | Platform library |
| 7 | `ControlToInvokeOn` / `BeginInvoke` | Not needed — callbacks run on event loop | No UI thread marshalling in JS |
| 8 | Separate NuGet packages per transport | Single package, tree-shakeable imports | npm convention |
| 9 | `Windows.Forms` samples | Not applicable (web UI or CLI) | Platform-specific |
| 10 | `Encoding.GetEncoding("ISO-8859-1")` | Custom latin-1 `string` ↔ `Uint8Array` helpers | Avoid Node/browser API drift and preserve byte-exact core |
| 11 | `ListQueue<T> : List<T>` | `ListQueue<T>` wrapping array | No list inheritance in JS |
| 12 | Multiple constructor overloads | Overload signatures + options object | TypeScript pattern |
| 13 | `event` keyword + delegate | `TypedEmitter` with `.on()`/`.off()` | JS EventEmitter idiom |

### Additional TypeScript-specific features (not in C#/Python):
- **`WebSerialTransport`** — browser Web Serial API (new transport)
- **`WebSocketTransport`** — browser/Node WebSocket (new transport)
- **Tree-shaking** — unused transports are excluded by bundlers
- **`ITransport` as interface** — TypeScript has true interfaces (unlike Python ABCs)
- **Generic `TypedEmitter<T>`** — fully type-safe events with autocomplete

---

## 12. Connection Settings Persistence

| Environment | Storer | Storage |
|-------------|--------|---------|
| Node.js | `JsonConnectionStorer` | `~/.cmdmessenger/connection.json` |
| Browser | `LocalStorageConnectionStorer` | `localStorage` |

```typescript
export interface ConnectionStorer {
  load(): Promise<Record<string, unknown> | null>;
  save(settings: Record<string, unknown>): Promise<void>;
  clear(): Promise<void>;
}
```
