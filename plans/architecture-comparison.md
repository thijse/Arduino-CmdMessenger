# Architecture Comparison: C# vs Python vs TypeScript

## 0. Porting Rule

The C# implementation is the behavioral reference for the Python and TypeScript
ports. Port implementations should mirror the C# public API shape, protocol
behavior, queue semantics, connection state machine, and test scenarios unless a
language/runtime convention requires a different expression.

Allowed deviations:
- Naming conventions: `PascalCase` in C#, `snake_case` in Python, `camelCase` in TypeScript.
- Concurrency primitives: C# threads/events, Python threads/events, TypeScript promises/event loop.
- Platform APIs: `System.IO.Ports`, `pyserial`, `serialport`, Node `net`, browser APIs.
- Packaging conventions: NuGet, PyPI, npm.

If the C# design appears suboptimal, do not silently fix only the port. Record it
under "Reference Codebase Suggestions" and decide whether the C# reference should
change first or whether the port may add a clearly documented, additive extension.

---

## 1. Structural Overview

All three ports share the same logical architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                         CmdMessenger (façade)                    │
│  attach / send / sendSync / queueCommand / strategies           │
├─────────────────────┬───────────────────────────────────────────┤
│  SendCommandQueue   │  ReceiveCommandQueue                      │
│  (drain loop)       │  (drain loop → dispatch callbacks)        │
├─────────────────────┴───────────────────────────────────────────┤
│                   CommunicationManager                          │
│  parse incoming bytes → ReceivedCommand / serialize outgoing    │
├─────────────────────────────────────────────────────────────────┤
│                   ITransport / Transport                        │
│  Serial / TCP / Bluetooth / WebSerial / WebSocket               │
├─────────────────────────────────────────────────────────────────┤
│                   ConnectionManager                             │
│  State machine: WAIT → SCAN → CONNECT → WATCHDOG               │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. File / Module Structure Comparison

| Concern | C# | Python | TypeScript |
|---------|-----|--------|-----------|
| Façade | `CmdMessenger.cs` | `cmd_messenger.py` | `cmdMessenger.ts` |
| Command base | `Command.cs` | `command.py` | `command.ts` |
| Send command | `SendCommand.cs` | `send_command.py` | `sendCommand.ts` |
| Received command | `ReceivedCommand.cs` | `received_command.py` | `receivedCommand.ts` |
| Communication | `CommunicationManager.cs` | `communication_manager.py` | `communicationManager.ts` |
| Connection mgr (abstract) | `ConnectionManager.cs` | `connection_manager.py` | `connectionManager.ts` |
| Worker / pump | `AsyncWorker.cs` | `async_worker.py` | *(inline async loops)* |
| Signal primitive | `EventWaiter.cs` | `event_waiter.py` | `eventWaiter.ts` |
| ACK signal | `ReceivedCommandSignal.cs` | `received_command_signal.py` | `receivedCommandSignal.ts` |
| Binary codec | `BinaryConverter.cs` | `binary_converter.py` | `binaryConverter.ts` |
| Escaping | `Escaped.cs` | `escaping.py` | `escaping.ts` |
| Enums | *(inline in classes)* | `enums.py` | `enums.ts` |
| Event system | C# `event` keyword | `event.py` (custom `Event` class) | `typedEmitter.ts` |
| Queue base | `Queue/CommandQueue.cs` | `queue/command_queue.py` | `queue/commandQueue.ts` |
| Send queue | `Queue/SendCommandQueue.cs` | `queue/send_command_queue.py` | `queue/sendCommandQueue.ts` |
| Receive queue | `Queue/ReceiveCommandQueue.cs` | `queue/receive_command_queue.py` | `queue/receiveCommandQueue.ts` |
| List queue | `Queue/ListQueue.cs` | `queue/list_queue.py` | `queue/listQueue.ts` |
| Strategy base | `Queue/CommandStrategy.cs` | `queue/command_strategy.py` | `queue/commandStrategy.ts` |
| Top strategy | `Queue/TopCommandStrategy.cs` | `queue/top_command_strategy.py` | `queue/topCommandStrategy.ts` |
| Collapse strategy | `Queue/CollapseCommandStrategy.cs` | `queue/collapse_command_strategy.py` | `queue/collapseCommandStrategy.ts` |
| General strategy | `Queue/GeneralStrategy.cs` | `queue/general_strategy.py` | `queue/generalStrategy.ts` |
| Stale strategy | `Queue/StaleGeneralStrategy.cs` | `queue/stale_general_strategy.py` | `queue/staleGeneralStrategy.ts` |
| Transport interface | `Transport/ITransport.cs` | `transport/transport.py` | `transport/transport.ts` |
| Serial transport | `Transport.Serial/SerialTransport.cs` | `transport/serial/serial_transport.py` | `transport/serial/serialTransport.ts` |
| Serial settings | `Transport.Serial/SerialSettings.cs` | `transport/serial/serial_settings.py` | `transport/serial/serialSettings.ts` |
| Serial conn mgr | `Transport.Serial/SerialConnectionManager.cs` | `transport/serial/serial_connection_manager.py` | `transport/serial/serialConnectionManager.ts` |
| TCP transport | `Transport.Network/TcpTransport.cs` | `transport/network/tcp_transport.py` | `transport/network/tcpTransport.ts` |
| TCP conn mgr | `Transport.Network/TcpConnectionManager.cs` | `transport/network/tcp_connection_manager.py` | `transport/network/tcpConnectionManager.ts` |
| Bluetooth | `Transport.Bluetooth/BluetoothTransport.cs` | *(planned)* | *(not planned)* |
| WebSerial | — | — | `transport/webserial/webSerialTransport.ts` |
| WebSocket | — | — | `transport/websocket/webSocketTransport.ts` |
| Loopback (test) | — | `tests/loopback_transport.py` | `transport/loopbackTransport.ts` |
| Settings storer | `ISerialConnectionStorer.cs` | `connection_storer.py` | *(ConnectionStorer interface)* |
| Logger | `Logger.cs` | `logger.py` | `logger.ts` |
| Time utils | `TimeUtils.cs` | `time_utils.py` | `timeUtils.ts` |

---

## 3. Class Hierarchy Comparison

```
C#:                              Python:                         TypeScript:
────                             ──────                         ──────────
ITransport (interface)           Transport (ABC)                 ITransport (interface)
├─ SerialTransport               ├─ SerialTransport              ├─ SerialTransport
├─ TcpTransport                  ├─ TcpTransport                 ├─ TcpTransport
├─ BluetoothTransport            └─ (future: Bluetooth)          ├─ WebSerialTransport
└─ (IPC placeholder)                                             ├─ WebSocketTransport
                                                                 └─ LoopbackTransport

Command                          Command                         Command
├─ SendCommand                   ├─ SendCommand                  ├─ SendCommand
└─ ReceivedCommand               └─ ReceivedCommand              └─ ReceivedCommand

CommandQueue (abstract)          CommandQueue (ABC)              CommandQueue (abstract)
├─ SendCommandQueue              ├─ SendCommandQueue             ├─ SendCommandQueue
└─ ReceiveCommandQueue           └─ ReceiveCommandQueue          └─ ReceiveCommandQueue

CommandStrategy                  CommandStrategy                 CommandStrategy
├─ TopCommandStrategy            ├─ TopCommandStrategy           ├─ TopCommandStrategy
└─ CollapseCommandStrategy       └─ CollapseCommandStrategy      └─ CollapseCommandStrategy

GeneralStrategy                  GeneralStrategy                 GeneralStrategy
└─ StaleGeneralStrategy          └─ StaleGeneralStrategy         └─ StaleGeneralStrategy

ConnectionManager (abstract)     ConnectionManager (ABC)         ConnectionManager (abstract)
├─ SerialConnectionManager       ├─ SerialConnectionManager      ├─ SerialConnectionManager
├─ TcpConnectionManager          ├─ TcpConnectionManager         └─ TcpConnectionManager
└─ BluetoothConnectionManager    └─ (future)
```

---

## 4. Concurrency Model Comparison

| Aspect | C# | Python | TypeScript |
|--------|-----|--------|-----------|
| **Concurrency model** | Multi-threaded (CLR threads) | Multi-threaded (daemon threads) | Single-threaded (event loop) |
| **Worker implementation** | `AsyncWorker` class (background `Thread`) | `AsyncWorker` class (daemon `Thread`) | Async drain loop (`async function`) |
| **Synchronization** | `lock {}` / `Monitor.Wait`/`Pulse` | `threading.Lock` / `threading.Condition` | Not needed (single-threaded) |
| **Signal primitive** | `EventWaiter` → `Monitor.Wait(timeout)` | `EventWaiter` → `threading.Event.wait(timeout)` | `EventWaiter` → `Promise` + resolver |
| **Blocking send** | `SendCommand()` blocks calling thread | `send_command()` blocks calling thread | `await sendCommand()` suspends coroutine |
| **Queue drain** | Background thread loops, sleeps when empty | Background thread loops, sleeps when empty | `await dequeue()` suspends until item posted |
| **Transport polling** | `AsyncWorker` with 1s read timeout | `AsyncWorker` with 1s read timeout | Event-driven (`'data'` events from serialport) |
| **Thread safety** | Locks on shared state | Locks on shared state | N/A — no concurrent access |

### Architectural Equivalences:

```
C# AsyncWorker          →  Python AsyncWorker        →  TS async drainLoop()
C# lock (_lock) { }     →  Python with self._lock:   →  (not needed)
C# Monitor.Wait(ms)     →  Python event.wait(s)      →  await new Promise(resolve => setTimeout(resolve, ms))
C# event += handler     →  Python event += handler   →  emitter.on('event', handler)
C# Thread.Start()       →  Python thread.start()     →  (loop starts on connect)
```

---

## 5. Event System Comparison

| Feature | C# | Python | TypeScript |
|---------|-----|--------|-----------|
| **Syntax** | `event EventHandler<T> Foo;` | `foo = Event()` | `events: TypedEmitter<{foo: (arg: T) => void}>` |
| **Subscribe** | `obj.Foo += handler;` | `obj.foo += handler` | `obj.events.on('foo', handler)` |
| **Unsubscribe** | `obj.Foo -= handler;` | `obj.foo -= handler` | `obj.events.off('foo', handler)` |
| **Fire** | `Foo?.Invoke(this, args);` | `foo(args)` | `this.events.emit('foo', args)` |
| **Type safety** | Delegate type | No (duck typing) | Full generic inference |
| **Multi-cast** | Built-in | Custom (list of handlers) | Built-in (emitter) |

---

## 6. Naming Convention Comparison

| Concept | C# | Python | TypeScript |
|---------|-----|--------|-----------|
| Class | `CmdMessenger` | `CmdMessenger` | `CmdMessenger` |
| Method | `SendCommand()` | `send_command()` | `sendCommand()` |
| Property | `PrintLfCr` | `print_lf_cr` | `printLfCr` |
| Private field | `_communicationManager` | `_communication_manager` | `_communicationManager` |
| Constant | `DefaultTimeout` | `DEFAULT_TIMEOUT` | `DEFAULT_TIMEOUT` |
| Interface | `ITransport` | `Transport` (ABC) | `ITransport` |
| Event | `NewLineReceived` | `new_line_received` | `'newLineReceived'` |
| Enum member | `SendQueue.InFrontQueue` | `SendQueue.IN_FRONT_QUEUE` | `SendQueue.InFrontQueue` |
| File name | `CmdMessenger.cs` | `cmd_messenger.py` | `cmdMessenger.ts` |
| Package | `CommandMessenger` (namespace) | `cmd_messenger` (package) | `cmd-messenger` (npm) |

---

## 7. Constructor / Initialization Comparison

### CmdMessenger construction:

```csharp
// C# — multiple overloads
var messenger = new CmdMessenger(transport, BoardType.Bit16);
var messenger = new CmdMessenger(transport, BoardType.Bit16, ',', ';', '/', 60);
```

```python
# Python — single __init__ with defaults
messenger = CmdMessenger(transport, board_type=BoardType.BIT_16)
messenger = CmdMessenger(transport, board_type=BoardType.BIT_16,
                         field_separator=',', command_separator=';',
                         escape_character='/', send_buffer_max_length=60)
```

```typescript
// TypeScript — single constructor with optional params
const messenger = new CmdMessenger(transport, BoardType.Bit16);
const messenger = new CmdMessenger(transport, BoardType.Bit16, ',', ';', '/', 60);
```

### SendCommand construction:

```csharp
// C# — many overloads
var cmd = new SendCommand(0);
var cmd = new SendCommand(0, "hello");
var cmd = new SendCommand(0, 1, 1000);  // cmdId, ackCmdId, timeout
```

```python
# Python — varargs + keyword args
cmd = SendCommand(0)
cmd = SendCommand(0, "hello")
cmd = SendCommand(0, ack_cmd_id=1, timeout=1000)
```

```typescript
// TypeScript — overload signatures
const cmd = new SendCommand(0);
const cmd = new SendCommand(0, "hello");
const cmd = new SendCommand(0, { ackCmdId: 1, timeout: 1000 });
```

---

## 8. Resource Management Comparison

| Pattern | C# | Python | TypeScript |
|---------|-----|--------|-----------|
| **Cleanup idiom** | `IDisposable` + `using` | `__enter__`/`__exit__` + `with` | `dispose()` + optional `using` (TC39) |
| **Deterministic cleanup** | `using var m = new CmdMessenger(...)` | `with CmdMessenger(...) as m:` | `{ using m = new CmdMessenger(...) }` (future) |
| **Manual cleanup** | `messenger.Dispose()` | `messenger.dispose()` | `messenger.dispose()` |
| **Worker shutdown** | `AsyncWorker.Stop()` → thread joins | `async_worker.stop()` → thread joins | Drain loop exits on flag |

---

## 9. Transport Abstraction Comparison

### Interface definition:

```csharp
// C# — interface
public interface ITransport : IDisposable {
    bool Connect();
    bool Disconnect();
    bool IsConnected();
    byte[] Read();
    void Write(byte[] buffer);
    event EventHandler DataReceived;
}
```

```python
# Python — ABC
class Transport(ABC):
    data_received: Event
    @abstractmethod def connect(self) -> bool: ...
    @abstractmethod def disconnect(self) -> bool: ...
    @abstractmethod def is_connected(self) -> bool: ...
    @abstractmethod def read(self) -> bytes: ...
    @abstractmethod def write(self, data: bytes) -> None: ...
```

```typescript
// TypeScript — interface
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

### Key differences:
- **C#/Python** `Connect()` is synchronous (blocks until connected)
- **TypeScript** `connect()` returns `Promise<boolean>` (async by nature)
- **C#/Python** `Write()` is synchronous
- **TypeScript** `write()` is async (serial/network writes are async in Node.js)
- **C#** uses `byte[]`, **Python** uses `bytes`, **TypeScript** uses `Uint8Array`

---

## 10. Send/ACK Flow Comparison

| Step | C# | Python | TypeScript |
|------|-----|--------|-----------|
| 1. Queue command | `SendCommandQueue` worker picks up | Same | Async drain loop picks up |
| 2. Suspend receive | `ReceiveCommandQueue.Suspend()` | `receive_command_queue.suspend()` | `receiveCommandQueue.suspend()` |
| 3. Arm signal | `ReceivedCommandSignal.PrepareForWait()` | `received_command_signal.prepare_for_wait()` | `receivedCommandSignal.prepareForWait()` |
| 4. Write to wire | `transport.Write(bytes)` | `transport.write(data)` | `await transport.write(data)` |
| 5. Wait for ACK | `EventWaiter.WaitOne(timeout)` blocks thread | `event_waiter.wait_one(timeout)` blocks thread | `await eventWaiter.waitOne(timeout)` suspends |
| 6. ACK arrives | Signal fires on receive thread | Signal fires on transport thread | Signal resolves Promise (same loop) |
| 7. Resume receive | `ReceiveCommandQueue.Resume()` | `receive_command_queue.resume()` | `receiveCommandQueue.resume()` |

---

## 11. Platform-Specific Differences

| Feature | C# | Python | TypeScript |
|---------|-----|--------|-----------|
| **Serial library** | `System.IO.Ports.SerialPort` | `pyserial` | `serialport` (npm) |
| **TCP library** | `System.Net.Sockets.TcpClient` | `socket` (stdlib) | `net` (Node.js) |
| **Bluetooth** | InTheHand.Net (3rd party) | PyBluez (planned) | Not planned |
| **Web Serial** | N/A | N/A | Web Serial API (browser) |
| **WebSocket** | N/A | N/A | Native WebSocket (browser/Node) |
| **Settings storage** | .NET `Properties.Settings` | JSON file | JSON file (Node) / localStorage (browser) |
| **UI samples** | WinForms + ZedGraph | FastAPI + WebSocket + Plotly.js | CLI / Web (same as Python) |
| **Package distribution** | NuGet (multiple packages) | PyPI (single package) | npm (single package) |
| **Separate transport packages** | Yes (NuGet per transport) | No (all in one) | No (all in one, tree-shakeable) |

---

## 12. Unique Features Per Port

### C# only:
- `ControlToInvokeOn` — UI thread marshalling for WinForms
- `BluetoothTransport` — full Bluetooth SPP support
- `StructSerializer` — struct-to-bytes utility
- Multiple NuGet packages (fine-grained deps)
- `SerialConnectionStorer` / `ISerialConnectionStorer` interface

### Python only:
- `ReceivedCommand.read("ifs")` — format-string reader
- `Event` class with `+=`/`-=` operator overloads
- `with` context manager support
- `JsonConnectionStorer` (cross-platform file persistence)
- `py.typed` for PEP 561 type checker support
- Web UI samples (FastAPI + Plotly.js)

### TypeScript only:
- `WebSerialTransport` — browser Web Serial API
- `WebSocketTransport` — browser/Node WebSocket
- `LoopbackTransport` — built-in test transport
- Full generic type safety on events (`TypedEmitter<T>`)
- Tree-shaking (unused transports excluded by bundlers)
- `LocalStorageConnectionStorer` (browser persistence)
- Works in both Node.js and browser from same package
- `Promise`-based API throughout (no blocking)

---

## 13. What's Identical Across All Three

These elements are preserved exactly across all ports:

1. **Wire protocol** — same escape characters, separators, binary encoding
2. **Class hierarchy** — same classes, same relationships
3. **Queue strategy pattern** — `CommandStrategy`, `TopCommandStrategy`, `CollapseCommandStrategy`, `GeneralStrategy`, `StaleGeneralStrategy`
4. **ConnectionManager state machine** — WAIT → SCAN → CONNECT → WATCHDOG
5. **Send batching** — non-ack commands accumulated up to `sendBufferMaxLength`
6. **ACK flow** — suspend receive queue, arm signal, send, wait, resume
7. **Callback dispatch** — `attach(cmdId, callback)` with default fallback
8. **`ReceivedCommand` cursor** — sequential argument reading with typed methods
9. **`BinaryConverter`** — same encoding for int16/32, float, double, bool
10. **`BoardType.Bit16` / `Bit32`** — controls int/float widths in binary mode

---

## 14. Decision Matrix: When to Choose Which Port

| Use Case | Recommended Port |
|----------|-----------------|
| Desktop app with .NET | C# |
| Cross-platform CLI / scripting | Python |
| Raspberry Pi / Linux headless | Python |
| Web dashboard (real-time) | TypeScript (browser) or Python (FastAPI) |
| Electron app | TypeScript |
| React Native / mobile | TypeScript (WebSocket transport) |
| Industrial / existing .NET stack | C# |
| Quick prototyping | Python |
| Full-stack JS project | TypeScript |
| Browser-only (no server) | TypeScript (WebSerial) |

---

## 15. Reference Codebase Suggestions

These are observations where a port may reveal a weakness in the C# reference.
They are not permission for a port to diverge silently.

### Existing observations

| Area | Current C# Behavior | Suggestion |
|------|---------------------|------------|
| `ConnectionManager` identify ACK | `ArduinoAvailable()` sends `identifyCommandId` and waits for the same command ID as ACK. | Keep ports compatible with this for now. If request/response pairs are desired, add an explicit C# API such as separate `identifyRequestCommandId` / `identifyResponseCommandId`, then port that shape. |
| Hardware board discovery | `BoardDiscovery` uses VID/PID shortcuts, then serial-probes remaining ports. | Avoid probing non-USB/Bluetooth COM ports where possible and keep discovery bounded by explicit timeouts so unavailable ports cannot stall a hardware run. |
| Serial line controls | Product `SerialSettings` exposes `DtrEnable`; the C# hardware-only test transport also sets `RtsEnable = true`. | If RTS is needed beyond test fixtures, add `RtsEnable` to C# `SerialSettings` and `SerialTransport`; otherwise avoid exposing it as a first-class port API. |
| Serial open robustness | `SerialTransport.Open()` calls `SerialPort.Open()` directly after `PortExists()`. | Consider a bounded/open-cancellation strategy or stricter discovery filters for platforms where opening stale virtual ports can hang or block for a long time. |

---

### Future improvement suggestions (inspired by Python port & TypeScript design)

#### A. Async core with `async`/`await` (`Task<T>` API)

**Current C# behavior**: `SendCommand()` blocks the calling thread with
`SpinWait.SpinUntil()` (busy-waits) until the ACK arrives or timeout expires.
The transport and queue workers use background `Thread` instances.

**Suggestion**: Introduce an `async Task<ReceivedCommand> SendCommandAsync()`
overload that awaits the ACK via `TaskCompletionSource<T>` instead of spinning.
This would:
- Eliminate busy-spin CPU waste (replace `SpinWait.SpinUntil` with `await`)
- Allow callers to use `async/await` (modern .NET idiom since C# 5.0)
- Enable concurrent sends from UI code without freezing the thread
- Align with TypeScript's `Promise`-based design
- Keep the existing sync `SendCommand()` as a backward-compatible wrapper

The Python port already replaces `SpinWait` with `time.sleep(0.001)` (less CPU),
and the TypeScript port is 100% `Promise`-based — demonstrating the pattern works.

#### B. Callback error isolation

**Current C# behavior**: User-attached callbacks are invoked directly; if a
callback throws, the exception propagates up through the receive queue worker and
can crash/hang the message pump.

**Suggestion**: Wrap callback invocations in `try/catch` (log + swallow), matching
what the Python port already does:
```python
try:
    callback(received_command)
except Exception:
    pass  # or log
```

This prevents a misbehaving user callback from killing the receive pipeline.
Optionally expose an `OnCallbackError` event for diagnostics.

#### C. Transport URL scheme (virtual/networked ports)

**Current C# behavior**: `SerialTransport` only accepts real COM port names.
Testing requires actual hardware or a subprocess-based fake.

**Suggestion**: Support URL-style port strings (like pyserial's `serial_for_url`):
- `loop://` — loopback for unit tests (no hardware)
- `socket://host:port` — TCP-bridged serial server (e.g. ser2net)
- `rfc2217://host:port` — Telnet-based remote serial (industrial equipment)

The Python port already supports this as a first-class feature via pyserial's URL
handling. It enables testing without hardware and remote serial access with zero
code changes — just a different port string.

#### D. Context-managed / `IAsyncDisposable` lifecycle

**Current C# behavior**: The user must call `Dispose()` manually (or use a
`using` block). Forgetting cleanup leaves zombie threads running.

**Suggestion**: Implement `IAsyncDisposable` in addition to `IDisposable`, so
`await using var messenger = new CmdMessenger(...)` works cleanly in modern
async C# code. The Python port already offers `with CmdMessenger(...) as m:`
context managers that guarantee cleanup even on exceptions.

#### E. `RtsEnable` in product `SerialSettings`

**Current C# behavior**: The product `SerialSettings` class exposes `DtrEnable`
but not `RtsEnable`. The test-only `SerialPortTransport` hard-codes
`RtsEnable = true`.

**Suggestion**: Add `RtsEnable` to the product `SerialSettings` and apply it in
`SerialTransport.Open()`. ESP32 and ESP8266 boards require both DTR and RTS for
the auto-reset circuit to work correctly. Without RTS in the product transport,
ESP boards fail to connect. The Python port now exposes both `dtr_enable` and
`rts_enable` in `SerialSettings`.

#### F. Reentrant lock in `CommunicationManager`

**Current C# behavior**: Uses `lock(_sendCommandDataLock)` — which is reentrant
by design in C# (`Monitor.Enter` is reentrant on the same thread).

**Observation**: This is already fine in C#. However, document this explicitly,
because the Python port needed to switch from `threading.Lock` to
`threading.RLock` to achieve the same behavior. This is a documentation/clarity
improvement, not a code change.

#### G. Replace `SpinWait.SpinUntil()` with event-based wait

**Current C# behavior**: Several places use `SpinWait.SpinUntil(predicate, ms)`
for waiting — e.g. waiting for send queue to empty, or waiting for worker thread
state transition.

**Suggestion**: Replace with `ManualResetEventSlim.Wait(ms)` or
`SemaphoreSlim.WaitAsync(ms)` — lower CPU usage, better for laptop battery life,
and more composable with async code. The Python port uses
`threading.Event.wait(timeout)` which is the equivalent non-spinning approach.

#### H. Format-string argument reader

**Current C# behavior**: `ReceivedCommand` has individual typed readers:
`ReadInt16Arg()`, `ReadInt32Arg()`, `ReadFloatArg()`, etc. Reading many args
requires multiple calls.

**Suggestion**: Consider adding a batch reader like Python's
`cmd.read("ifs")` (int, float, string) which returns a tuple of all values in
one call. This is syntactic sugar and optional, but makes multi-argument commands
more concise:
```csharp
// Today:
var name = reply.ReadStringArg();
var temp = reply.ReadFloatArg();
var count = reply.ReadInt32Arg();

// Proposed:
var (name, temp, count) = reply.ReadArgs<string, float, int>();
```

#### I. Unified test infrastructure across languages

**Current state**: C# and Python now have aligned test structures (shared
scenarios, per-board parametrization, auto-discovery), but the test *runner*
invocation differs (`dotnet test --filter` vs `pytest --hardware`).

**Suggestion**: Standardize on a single repo-root `test.bat` / `test.sh` that
can run all languages with the same flags:
```
test.bat --hardware          # runs C# + Python hardware tests
test.bat --python            # Python only
test.bat --csharp            # C# only
test.bat --all               # everything
```

#### J. Structured logging instead of `Console.WriteLine`

**Current C# behavior**: Debug output uses `Console.WriteLine` or custom
`Logger` class with limited configurability.

**Suggestion**: Use `Microsoft.Extensions.Logging.ILogger` for structured
logging with log levels, scopes, and pluggable sinks. The Python port uses
a custom `Logger` class; both could benefit from structured logging to aid
production diagnostics (especially connection state machine transitions and
ACK timeout debugging).

#### K. Cancellation token support

**Current C# behavior**: Long-running operations (connect, send with ack,
discovery) have fixed timeouts but no cancellation support.

**Suggestion**: Accept `CancellationToken` on async methods:
```csharp
Task<ReceivedCommand> SendCommandAsync(SendCommand cmd, CancellationToken ct = default);
Task<bool> ConnectAsync(CancellationToken ct = default);
```

This enables cooperative cancellation from UI buttons, app shutdown, or test
timeouts — a standard modern .NET pattern that the current API lacks.

#### L. Migrate transport/sample .csproj to SDK-style

**Current C# behavior**: The core `CommandMessenger.csproj` is modern SDK-style
(multi-targets `net40;net8.0-windows`), but all transport projects
(`CommandMessenger.Transport.Serial`, `.Network`, `.Bluetooth`, `.IPC`) and all
sample projects still use old-style verbose MSBuild XML (explicit `PropertyGroup`
per configuration, `TargetFrameworkVersion v4.0`, manual file includes).

**Suggestion**: Convert transports and samples to SDK-style `.csproj`. Benefits:
- 80% less XML (SDK-style auto-includes `.cs` files)
- Multi-targeting becomes trivial (`<TargetFrameworks>net40;net8.0-windows</TargetFrameworks>`)
- `dotnet build` / `dotnet pack` work cleanly without needing legacy `msbuild`
- Consistent with the core library and test projects (which are already SDK-style)
- NuGet package generation becomes a one-liner (`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`)

The Python port has zero build-system complexity (just `pyproject.toml`). The TS
port will use a single `tsconfig.json`. The C# old-style projects are the only
remaining manual-maintenance burden in the repo.

#### M. Consolidate NuGet into single package (like Python/TS)

**Current C# behavior**: Transports are separate NuGet packages
(`CommandMessenger.Transport.Serial`, `.Network`, `.Bluetooth`), each with its
own `.nuspec` and release cycle.

**Suggestion**: Ship as a single NuGet package `CommandMessenger` with all
transports included, matching the Python (single PyPI package) and TypeScript
(single npm package) approach. Rationale:
- Eliminates version-matrix issues between core and transports
- Simplifies consumer install (`Install-Package CommandMessenger` — done)
- Transport dependencies (e.g. `InTheHand.Net` for Bluetooth) can be marked as
  optional via `<PrivateAssets>` or conditional compilation
- The total code volume across all transports is small (~500 lines each)
- Consumers who don't use Bluetooth never call the Bluetooth code, so unused
  transport DLLs don't load at runtime

If per-transport separation is still desired, use a single repo/package with
conditional compile constants (`BLUETOOTH_SUPPORT`) rather than separate projects.

#### N. Remove WinForms coupling from core library

**Current C# behavior**: `CmdMessenger.cs` accepts `Control controlToInvokeOn`
and uses `Control.BeginInvoke()` for UI thread marshalling. This makes the core
library depend on `System.Windows.Forms` and triggers CA1416 platform warnings
on non-Windows targets.

**Suggestion**: Replace with `SynchronizationContext.Current?.Post()` or accept a
custom `Action<Action> invokeOnMainThread` delegate. This:
- Removes the WinForms reference from the core library
- Works with WPF (`DispatcherSynchronizationContext`), MAUI, Avalonia, etc.
- Eliminates CA1416 warnings when targeting cross-platform .NET
- Python has no UI coupling at all (web UI is a separate sample)
- TypeScript uses standard event dispatch (no UI framework dependency)

Backward-compat wrapper:
```csharp
// Old (keep as extension method or overload):
messenger.ControlToInvokeOn = myForm;
// New:
messenger.CallbackContext = SynchronizationContext.Current;
```

#### O. Migrate legacy CommandMessengerTests to xUnit

**Current C# behavior**: `Tests/CommandMessengerTests/` is an old-style .NET 4.0
project that appears to be a manual test runner (executable, not a test framework
project). Meanwhile, `Tests/CommandMessenger.Tests/` and
`Tests/CommandMessenger.IntegrationTests/` are modern xUnit + net8.0 projects.

**Suggestion**: Port any remaining useful test logic from `CommandMessengerTests`
into the xUnit project, then remove the legacy project. This eliminates:
- A confusing duplicate test project in the solution
- An old-style .csproj that's inconsistent with the rest
- Manual test execution (xUnit integrates with `dotnet test`, VS Test Explorer, CI)

The Python port has a single `tests/` folder with pytest. The TypeScript port
will have a single `tests/` folder with vitest. C# should likewise have one
canonical test project per concern (unit + integration).

#### P. Replace vendored ZedGraph DLL with NuGet reference

**Current C# behavior**: `ZedGraph/ZedGraph.dll` is checked into the repository
as a binary. Sample projects reference it via `<HintPath>..\..\ZedGraph\ZedGraph.dll</HintPath>`.

**Suggestion**: Reference `ZedGraph` from NuGet (`ZedGraph` package exists) or
replace with a modern charting library. Vendored binaries:
- Can't be audited for vulnerabilities
- Don't get updates
- Bloat the git history with binaries
- Break on target framework changes

The Python port uses Plotly.js (loaded from CDN in the web UI). The TypeScript
port will likely do the same. The C# samples could use LiveCharts2, ScottPlot,
or OxyPlot — all available via NuGet and cross-platform.

#### Q. Add a web-based sample (SignalR or WebSocket)

**Current C# behavior**: All C# samples are WinForms desktop applications
requiring Windows.

**Suggestion**: Add a web-based sample using ASP.NET Core + SignalR (or raw
WebSocket), mirroring the Python `temperature_control` sample that uses
FastAPI + WebSocket + Plotly.js. This would:
- Demonstrate cross-platform usage (runs on Linux/macOS)
- Show real-time data streaming to a browser
- Serve as the natural bridge for the TypeScript WebSocket transport
- Provide a modern alternative for users who can't use WinForms

Architecture: ASP.NET Minimal API + SignalR hub → `CmdMessenger` →
`SerialTransport` → Arduino. Browser connects via SignalR and receives live data.

#### R. Event-driven transport instead of polling

**Current C# behavior**: `SerialTransport` runs an `AsyncWorker` that polls
`SerialPort.Read()` with a 1-second timeout in a loop. This wastes a thread
and adds up to 1s latency on message receipt.

**Suggestion**: Use `SerialPort.DataReceived` event (fires on the ThreadPool
when bytes arrive) and buffer into the receive pipeline immediately. This:
- Eliminates the dedicated polling thread
- Reduces receive latency to near-zero (event fires as soon as bytes available)
- Aligns with how both Python (`pyserial` with threading) and TypeScript
  (`serialport` 'data' events) handle incoming data
- Frees the `AsyncWorker` pattern for send-only queuing

The C# `SerialPort.DataReceived` event is known to be unreliable on some
platforms, so keep the polling approach as a fallback, but default to event-driven
where supported.

#### S. Board auto-detection via VID/PID without full serial probe

**Current C# behavior**: `SerialConnectionManager` scans all COM ports by
opening them, sending an identify command, and waiting for a response. This is
slow and can interfere with other devices.

**Suggestion**: First filter by USB VID/PID (using WMI on Windows, udev on
Linux) to find likely Arduino boards, then only probe those ports. The Python
port's `serial.tools.list_ports` provides `vid`/`pid`/`description` metadata
without opening the port. C# can use:
- Windows: `System.Management` WMI query on `Win32_PnPEntity`
- Linux: `/sys/class/tty/*/device/../../idVendor`
- Cross-platform: `System.IO.Ports.SerialPort.GetPortNames()` + registry lookup

This reduces scan time from O(all_ports × timeout) to O(matching_ports × timeout)

---

## 16. Implemented Decisions Log

Decisions that have been made and implemented, recorded here so future sessions
have context for why things are the way they are.

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-06-08 | `RtsEnable` added to C# `SerialSettings` and `SerialTransport` | ESP32/ESP8266 auto-reset requires both DTR and RTS; Python and TypeScript already had it |
| 2026-06-08 | `ReadCharArg()` added to C# `ReceivedCommand` | Python and TypeScript both had it; C# was the odd one out |
| 2026-06-08 | `Read(string format)` format-string reader added to C# `ReceivedCommand` | Python-originated API now consistent across all three ports |
| 2026-06-08 | `IEnumerable<string>` added to C# `ReceivedCommand` | Python has `__iter__`, TypeScript has `[Symbol.iterator]`; C# now has `foreach` |
| 2026-06-08 | `System.Windows.Forms` removed from core C# library | Replaced `ControlToInvokeOn` (WinForms `Control`) with `SynchronizationContext`; works with any UI framework and on Linux/macOS |
| 2026-06-08 | C# `BinaryFormatter` in storers replaced with JSON | `BinaryFormatter` is obsolete/removed in .NET 9; JSON is cross-platform and human-readable |
| 2026-06-08 | `SerialConnectionStorer` → `JsonSerialConnectionStorer` in Python | Align naming with C# pattern (transport-specific storer) |
| 2026-06-08 | Callback exception isolation added to C# `CmdMessenger` | Unhandled callback exceptions previously crashed the receive pump; now caught and surfaced via `CallbackException` event |
| 2026-06-08 | `ReceivedCommandSignal.ProcessCommand` fixed: non-matching commands no longer dropped | With `SendQueue.ClearQueue` active, commands arriving during ACK wait were silently discarded; fixed to always queue non-ACK commands |

### Phase 1 project structure (2026-06-08)

| Project | Before | After |
|---------|--------|-------|
| `CommandMessenger` (core) | `net40;net8.0-windows` | `netstandard2.0` |
| `Transport.Serial` | `net40;net8.0-windows` | `netstandard2.0` + `System.IO.Ports` NuGet |
| `Transport.Network` | Old MSBuild, `net40` | SDK-style, `netstandard2.0` |
| `Transport.Bluetooth` | Old MSBuild, `net40` | SDK-style, `net8.0-windows` (InTheHand is Windows-only) |
| Console samples (1–4, 7) | Old MSBuild, `net40` | SDK-style, `net8.0` |
| WinForms samples (5, 6, 9, 10) | Old MSBuild, `net40` | SDK-style, `net8.0-windows` |
| Bluetooth sample (8) | Old MSBuild, `net40` | SDK-style, `net8.0-windows` |
| `CommandMessenger.Tests` | `net8.0-windows` | `net8.0` (no WinForms dep needed) |

**`netstandard2.0` runtime coverage**: .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5–11, Mono, Unity.
Includes Linux and macOS via .NET Core/.NET 5+ runtimes.

### Phase 2 implemented (2026-06-08, commit 1ee3573)

| File | Change |
|------|--------|
| `EventWaiter.cs` | `Thread.Monitor` → `SemaphoreSlim(0,1)`; adds `WaitOneAsync()` |
| `AsyncWorker.cs` | Bare `Thread` → `async Task` drain loop + `CancellationTokenSource`; `AsyncWorkerJob` returns `Task<bool>` |
| `ReceivedCommandSignal.cs` | Replaced with empty stub — ACK waiting moved to `CommunicationManager` |
| `CommandQueue.cs` | `ProcessQueue()` abstract: `bool` → `Task<bool>` |
| `SendCommandQueue.cs` | `ProcessQueue()` and `SendCommandsFromQueue()` async |
| `ReceiveCommandQueue.cs` | `ProcessQueue()` returns `Task.FromResult()`; suspend/resume ACK mechanism removed; `PrepareForCmd`/`WaitForCmd` are noops |
| `CommunicationManager.cs` | `ConcurrentDictionary<int, TCS>` for ACK waits; `ProcessLine` checks TCS before enqueueing; `ExecuteSendCommandAsync` added; no more queue suspension |
| `CmdMessenger.cs` | `SendCommandAsync()` added; sync wrappers preserved |
| `ConnectionManager.cs` | `DoWork()` → `async Task<bool>` with `await Task.Delay(100)` |
| `SerialTransport.cs` | `Poll()` → `Task.FromResult(true)` |

Added `System.Threading.Channels` NuGet (netstandard2.0 compatible).

### Phase 2 plan (async core rewrite — not yet implemented)

Agreed design:
- Replace `AsyncWorker` (Thread-based) with `async Task` drain loops
- Replace `EventWaiter` (Monitor-based) with `SemaphoreSlim.WaitAsync()` (netstandard2.0 compatible)
- Replace suspend/resume + `ReceivedCommandSignal` ACK wait with per-ACK `TaskCompletionSource<ReceivedCommand>` (Option B — no queue suspension needed)
- `CommunicationManager.ExecuteSendCommand` becomes `async Task<ReceivedCommand>`
- `CmdMessenger.SendCommand` becomes `async Task<ReceivedCommand>`; old sync overload removed (breaking, accepted)
- Add `System.Threading.Channels` NuGet for `Channel<CommandStrategy>` as the queue backing
- `CancellationToken` support on all async send paths
- Nullable reference types cleaned up throughout
and eliminates interference with unrelated serial devices.
