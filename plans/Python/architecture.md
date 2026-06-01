# Python CmdMessenger Library — Architecture Plan

## 0. Decisions Locked In (2026-06-01)

| # | Decision | Choice |
|---|---------|--------|
| 1a | PyPI distribution name | `py-cmdmessenger` |
| 1b | Python import package name | `cmd_messenger` (matches C# main class `CmdMessenger`) |
| 1c | Python version floor | 3.9 |
| 1d | Build backend | `hatchling` via `pyproject.toml` |
| 1e | Repo location | `extras/Python/` (self-contained Python project) |
| 2 | Threading model | **Threading core + asyncio adapter** — see §0.1 |
| 3 | Event API | Tiny `Event` helper class with `+=` / `-=` operator overloads (see §0.2) |
| 4a | `bool` encoding in protocol | Lock down via tests: C# sends `"1"`/`"0"`; mirror exactly |
| 4b | String encoding | ISO-8859-1 (latin-1) — byte-exact with C# |
| 4c | Binary endianness | Little-endian explicit (`struct.pack('<...')`) |
| 4d | Double on Bit16 boards | Encoded/decoded as float — mirror C# |
| 5 | Tests | Deferred — implement after library works |
| 6 | Docstring style | Google |
| 7a | `SendCommand` argument API | Varargs (`SendCommand(0, 1, 2.5, "hi")`) |
| 7b | `ReceivedCommand` reader API | **Format-string** (e.g. `cmd.read("ifs")`) — divergence from C# (see §0.3) |
| 7c | Connect-failure behaviour | Raise `ConnectionError` (Pythonic); keep `is_connected()` for status |
| 8a | License | MIT (same as C# library) |
| 8b | Attribution | Credit `PyCmdMessenger` (harmsm) in README as inspiration |
| 8c | Copyright header | Match C# style, mark 2026 |

### Notes to backport to C# / new C# library
- **Varargs `SendCommand`** — modern C# can use `params object[]` for a similar fluent constructor; add to roadmap.
- **Format-string readers** — could be added as extension methods on `ReceivedCommand` in a future C# version.

### 0.1 Threading model rationale

**Choice: threading core + asyncio adapter.**

| Aspect | Threading core | Fully asyncio |
|--------|---------------|---------------|
| Matches C# 1:1 | ✅ direct port of `AsyncWorker`, `lock`, `Sleep`, `Wait` | ❌ requires rewriting all synchronization primitives |
| Blocking `send_command()` works in plain scripts | ✅ | ❌ user must `await` everything, even for "hello world" |
| Integration with FastAPI/WebSocket samples | Via `loop.call_soon_threadsafe()` in callbacks (~5 lines) | Native |
| Queue + command collapsing + strategies | Implemented with `threading.Lock` — direct port | Would need an async equivalent, but **still needs a single-task event-loop runner** (as user noted) for ordering — i.e. essentially the same architecture |
| Console samples | Trivial — match C# minimalism | Awkward — every sample needs an `asyncio.run()` shell |

> The user correctly observed: **even with full asyncio, you still need a "main async runner" that owns the queue, enforces sequentiality, and runs collapse/stale strategies.** That runner is structurally identical to our `SendCommandQueue` worker — so going asyncio buys very little while making the console samples uglier and breaking the C# parity goal.

The asyncio adapter (used by web samples) is a small `AsyncCmdMessenger` wrapper or simply `asyncio.to_thread()` around blocking calls + `loop.call_soon_threadsafe()` from receive callbacks.

### 0.2 Event helper class

Tiny reference implementation, modelled after the well-known C#-style pattern (see e.g. [this StackOverflow answer](https://stackoverflow.com/questions/1092531/event-system-in-python) and how libraries like `blinker` solve the same problem):

```python
class Event:
    """C#-style multicast event with += / -= operator overloads.

    Usage:
        messenger.new_line_received += my_handler
        messenger.new_line_received -= my_handler
        messenger.new_line_received(command)   # fires all handlers
    """

    def __init__(self):
        self._handlers: list[Callable] = []

    def __iadd__(self, handler):
        self._handlers.append(handler)
        return self

    def __isub__(self, handler):
        self._handlers.remove(handler)
        return self

    def __call__(self, *args, **kwargs):
        for handler in list(self._handlers):  # copy to allow modification during fire
            handler(*args, **kwargs)
```

For richer needs (thread-safety, weak refs, async handlers) we'd swap to `blinker`, but the 15-line version is enough.

### 0.3 Format-string readers (divergence from C#)

```python
# C# style (preserved as fallback):
cmd_id   = received.read_int32_arg()
value    = received.read_float_arg()
label    = received.read_string_arg()

# Pythonic format-string style (primary API, à la PyCmdMessenger):
cmd_id, value, label = received.read("ifs")
```

Format codes (will mirror PyCmdMessenger for familiarity):
| Code | Type | Notes |
|------|------|-------|
| `i` | int (signed 32-bit on protocol) | |
| `I` | unsigned int | |
| `b` | byte (signed) | |
| `B` | unsigned byte | |
| `l` | long (int64) | |
| `L` | unsigned long | |
| `f` | float | |
| `d` | double | float on Bit16 |
| `s` | string | latin-1 |
| `?` | bool | |
| `c` | single char | |
| Uppercase variants prefixed with `*` | binary form | e.g. `*f` = binary float |
| `*` (trailing) | repeat last code for remaining args | |

Both APIs coexist — the iterator-style `read_int32_arg()` etc. stays available for C#-style code paths.

---

## 1. Overview

A Pythonic port of the C#/VB `CommandMessenger` library, preserving the same class hierarchy, separation of concerns, and naming conventions while adapting to Python idioms.

**Distribution name (PyPI):** `py-cmdmessenger`
**Import name:** `cmd_messenger`

---

## 2. Package Layout

```
cmd_messenger/
├── __init__.py                     # Re-exports: CmdMessenger, SendCommand, ReceivedCommand, enums
├── cmd_messenger.py                # CmdMessenger (main façade)
├── command.py                      # Command base class
├── send_command.py                 # SendCommand
├── received_command.py             # ReceivedCommand
├── communication_manager.py        # CommunicationManager
├── connection_manager.py           # ConnectionManager (abstract base)
├── binary_converter.py             # BinaryConverter
├── escaping.py                     # Escaping + IsEscaped
├── event_waiter.py                 # EventWaiter (threading.Event wrapper)
├── received_command_signal.py      # ReceivedCommandSignal
├── time_utils.py                   # TimeUtils
├── logger.py                       # Logger
├── queue/
│   ├── __init__.py
│   ├── command_queue.py            # CommandQueue (abstract base)
│   ├── send_command_queue.py       # SendCommandQueue
│   ├── receive_command_queue.py    # ReceiveCommandQueue
│   ├── list_queue.py              # ListQueue (deque wrapper)
│   ├── command_strategy.py         # CommandStrategy
│   ├── collapse_command_strategy.py# CollapseCommandStrategy
│   ├── top_command_strategy.py     # TopCommandStrategy
│   ├── general_strategy.py         # GeneralStrategy
│   └── stale_general_strategy.py   # StaleGeneralStrategy
└── transport/
    ├── __init__.py
    ├── transport.py                # ITransport → Transport (ABC)
    ├── serial/
    │   ├── __init__.py
    │   ├── serial_transport.py     # SerialTransport
    │   ├── serial_settings.py      # SerialSettings (dataclass)
    │   ├── serial_connection_manager.py  # SerialConnectionManager
    │   └── serial_utils.py         # SerialUtils
    └── network/
        ├── __init__.py
        ├── tcp_transport.py        # TcpTransport
        └── tcp_connection_manager.py # TcpConnectionManager
```

---

## 3. Class & Function Design

### 3.1 Enums (`__init__.py` or dedicated `enums.py`)

```python
from enum import Enum, auto

class SendQueue(Enum):
    DEFAULT = auto()
    IN_FRONT_QUEUE = auto()
    AT_END_QUEUE = auto()
    WAIT_FOR_EMPTY_QUEUE = auto()
    CLEAR_QUEUE = auto()

class ReceiveQueue(Enum):
    DEFAULT = auto()
    WAIT_FOR_EMPTY_QUEUE = auto()
    CLEAR_QUEUE = auto()

class UseQueue(Enum):
    USE_QUEUE = auto()
    BYPASS_QUEUE = auto()

class BoardType(Enum):
    BIT_16 = auto()
    BIT_32 = auto()
```

### 3.2 `Transport` (ABC) — `transport/transport.py`

```python
from abc import ABC, abstractmethod
from typing import Callable

class Transport(ABC):
    """Interface for the transport layer (mirrors C# ITransport)."""

    on_data_received: Callable[[], None] | None  # event callback

    @abstractmethod
    def connect(self) -> bool: ...

    @abstractmethod
    def disconnect(self) -> bool: ...

    @abstractmethod
    def is_connected(self) -> bool: ...

    @abstractmethod
    def read(self) -> bytes: ...

    @abstractmethod
    def write(self, data: bytes) -> None: ...
```

### 3.3 `Command` — `command.py`

```python
class Command:
    cmd_id: int
    arguments: list[str]       # CmdArgs equivalent
    time_stamp: int            # millis

    @property
    def ok(self) -> bool: ...

    def command_string(self) -> str: ...
```

### 3.4 `SendCommand` — `send_command.py`

```python
class SendCommand(Command):
    req_ac: bool               # Request acknowledgement
    ack_cmd_id: int
    timeout: int               # ms

    def __init__(self, cmd_id: int, *args,
                 ack_cmd_id: int | None = None,
                 timeout: int = 0): ...

    # Fluent argument builders
    def add_argument(self, value) -> 'SendCommand': ...
    def add_bin_argument(self, value) -> 'SendCommand': ...
```

### 3.5 `ReceivedCommand` — `received_command.py`

```python
class ReceivedCommand(Command):
    raw_string: str

    # Iterator-style typed argument readers (mirror C# ReadXxxArg)
    def read_int16_arg(self) -> int: ...
    def read_uint16_arg(self) -> int: ...
    def read_int32_arg(self) -> int: ...
    def read_uint32_arg(self) -> int: ...
    def read_float_arg(self) -> float: ...
    def read_double_arg(self) -> float: ...
    def read_string_arg(self) -> str: ...
    def read_bool_arg(self) -> bool: ...

    # Binary argument readers
    def read_bin_int16_arg(self) -> int: ...
    def read_bin_uint16_arg(self) -> int: ...
    def read_bin_int32_arg(self) -> int: ...
    def read_bin_uint32_arg(self) -> int: ...
    def read_bin_float_arg(self) -> float: ...
    def read_bin_double_arg(self) -> float: ...

    # Pythonic extras
    def available(self) -> bool: ...
    def __iter__(self): ...        # iterate over string args
```

### 3.6 `CommunicationManager` — `communication_manager.py`

```python
class CommunicationManager:
    field_separator: str
    command_separator: str
    escape_character: str
    board_type: BoardType
    print_lf_cr: bool
    last_line_time_stamp: int

    def __init__(self, transport, receive_command_queue, board_type,
                 command_separator, field_separator, escape_character): ...

    def connect(self) -> bool: ...
    def disconnect(self) -> bool: ...
    def write(self, value: str) -> None: ...
    def write_line(self, value: str) -> None: ...
    def execute_send_command(self, send_command, send_queue_state) -> ReceivedCommand: ...
```

### 3.7 `CmdMessenger` — `cmd_messenger.py`

```python
class CmdMessenger:
    """Main façade — mirrors C# CmdMessenger class."""

    # Events (Python callbacks / observer pattern)
    on_new_line_received: Callable[[Command], None] | None
    on_new_line_sent: Callable[[Command], None] | None

    print_lf_cr: bool

    def __init__(self, transport: Transport,
                 board_type: BoardType = BoardType.BIT_16,
                 field_separator: str = ',',
                 command_separator: str = ';',
                 escape_character: str = '/',
                 send_buffer_max_length: int = 60): ...

    def connect(self) -> bool: ...
    def disconnect(self) -> bool: ...

    # Callback registration (mirrors C# Attach)
    def attach(self, callback: Callable[[ReceivedCommand], None],
               message_id: int | None = None) -> None: ...

    # Send
    def send_command(self, send_command: SendCommand,
                     send_queue_state: SendQueue = SendQueue.IN_FRONT_QUEUE,
                     receive_queue_state: ReceiveQueue = ReceiveQueue.DEFAULT,
                     use_queue: UseQueue = UseQueue.USE_QUEUE) -> ReceivedCommand: ...

    def send_command_sync(self, send_command: SendCommand,
                          send_queue_state: SendQueue) -> ReceivedCommand: ...

    def queue_command(self, send_command: SendCommand | CommandStrategy) -> None: ...

    # Strategy hooks
    def add_receive_command_strategy(self, strategy: GeneralStrategy) -> None: ...
    def add_send_command_strategy(self, strategy: GeneralStrategy) -> None: ...

    # Queue management
    def clear_receive_queue(self) -> None: ...
    def clear_send_queue(self) -> None: ...

    @property
    def last_received_command_time_stamp(self) -> int: ...

    # Context manager support
    def __enter__(self) -> 'CmdMessenger': ...
    def __exit__(self, *exc) -> None: ...
    def dispose(self) -> None: ...
```

### 3.8 `ConnectionManager` (ABC) — `connection_manager.py`

```python
class ConnectionManager(ABC):
    on_connection_found: Callable[[], None] | None
    on_connection_timeout: Callable[[], None] | None
    on_progress: Callable[[int, str], None] | None

    connected: bool
    watchdog_timeout: int
    watchdog_retry_timeout: int
    watchdog_tries: int
    watchdog_enabled: bool
    device_scan_enabled: bool
    persistent_settings: bool

    def __init__(self, cmd_messenger: CmdMessenger,
                 identify_command_id: int = 0,
                 unique_device_id: str | None = None): ...

    def start_connection_manager(self) -> None: ...
    def stop_connection_manager(self) -> None: ...

    @abstractmethod
    def _do_work_connect(self) -> None: ...

    @abstractmethod
    def _do_work_scan(self) -> None: ...
```

### 3.9 Queue Classes — `queue/`

| Class | Methods |
|-------|---------|
| `CommandQueue` (ABC) | `start()`, `stop()`, `suspend()`, `resume()`, `clear()`, `count`, `is_empty`, `add_general_strategy()` |
| `SendCommandQueue(CommandQueue)` | `send_command()`, `queue_command()` |
| `ReceiveCommandQueue(CommandQueue)` | `queue_command()`, `dequeue_command()`, `prepare_for_cmd()`, `wait_for_cmd()` |
| `ListQueue` | `enqueue()`, `enqueue_front()`, `dequeue()`, `peek()` |
| `CommandStrategy` | `command`, `enqueue()`, `dequeue()` |
| `CollapseCommandStrategy(CommandStrategy)` | overrides `enqueue()` |
| `TopCommandStrategy(CommandStrategy)` | overrides `enqueue()` |
| `GeneralStrategy` | `on_enqueue()`, `on_dequeue()` |
| `StaleGeneralStrategy(GeneralStrategy)` | `__init__(command_timeout)`, overrides `on_dequeue()` |

### 3.10 Transport Implementations

#### `SerialTransport(Transport)` — `transport/serial/serial_transport.py`

```python
class SerialTransport(Transport):
    current_serial_settings: SerialSettings

    def connect(self) -> bool: ...
    def disconnect(self) -> bool: ...
    def is_connected(self) -> bool: ...
    def read(self) -> bytes: ...
    def write(self, data: bytes) -> None: ...
```

#### `SerialSettings` — `transport/serial/serial_settings.py`

```python
@dataclass
class SerialSettings:
    port_name: str = ''
    baud_rate: int = 9600
    parity: str = 'none'        # 'none', 'even', 'odd'
    data_bits: int = 8
    stop_bits: float = 1        # 1, 1.5, 2
    dtr_enable: bool = False
    timeout: float = 0.5        # seconds (pyserial uses float seconds)

    def is_valid(self) -> bool: ...
```

#### `SerialConnectionManager(ConnectionManager)` — `transport/serial/serial_connection_manager.py`

```python
class SerialConnectionManager(ConnectionManager):
    available_serial_ports: list[str]
    device_scan_baud_rate_selection: bool

    def __init__(self, serial_transport, cmd_messenger, ...): ...
```

#### `TcpTransport(Transport)` — `transport/network/tcp_transport.py`

```python
class TcpTransport(Transport):
    host: str
    port: int
    timeout: float

    def __init__(self, host: str, port: int): ...
    def connect(self) -> bool: ...
    def disconnect(self) -> bool: ...
    def is_connected(self) -> bool: ...
    def read(self) -> bytes: ...
    def write(self, data: bytes) -> None: ...
```

### 3.11 Utilities

| Module | Class/Function | Purpose |
|--------|---------------|---------|
| `escaping.py` | `IsEscaped`, `Escaping` | Escape/unescape protocol characters |
| `binary_converter.py` | `BinaryConverter` | Convert Python values ↔ binary-escaped strings |
| `event_waiter.py` | `EventWaiter` | `threading.Event` based signal with timeout |
| `received_command_signal.py` | `ReceivedCommandSignal` | Block-till-ack synchronization |
| `time_utils.py` | `TimeUtils` | `millis()`, `has_expired()` |
| `logger.py` | `Logger` | Optional file logger |

---

## 4. Threading Model

The C# library uses `AsyncWorker` (background threads + signal). In Python:

- **`threading.Thread`** (daemon) replaces `AsyncWorker`
- **`threading.Event`** replaces `EventWaiter`
- **`threading.Lock`** replaces C# `lock` statements
- **`collections.deque`** (thread-safe append/popleft) can back `ListQueue`

A private `_AsyncWorker` helper class will wrap this pattern to keep the same structure.

---

## 5. Deviations from C# Architecture

| # | C# Design | Python Adaptation | Reason |
|---|-----------|-------------------|--------|
| 1 | PascalCase methods (`SendCommand`, `ReadInt32Arg`) | snake_case (`send_command`, `read_int32_arg`) | PEP 8 convention |
| 2 | PascalCase properties (`PrintLfCr`, `CmdId`) | snake_case (`print_lf_cr`, `cmd_id`) | PEP 8 convention |
| 3 | `ITransport` interface | `Transport` ABC (abstract base class) | Python has no interfaces; ABCs serve the same role |
| 4 | `IDisposable` / `Dispose()` pattern | Context manager (`__enter__`/`__exit__`) + `dispose()` method | Pythonic resource management |
| 5 | C# events (`event EventHandler<T>`) | Callback attributes + optional list-of-callbacks pattern | Python has no built-in event system |
| 6 | Many constructor overloads (`SendCommand(int, string)`, `SendCommand(int, float)`, etc.) | Single `__init__` with `*args` + `add_argument()` fluent API | Python doesn't have overloads; duck-typing handles types |
| 7 | `Windows.Forms.Control.BeginInvoke` for UI thread marshalling | Removed entirely (not applicable) | Python GUIs use their own event loop integration |
| 8 | `ControlToInvokeOn` property | Not included | Platform-specific UI concern |
| 9 | Separate unsigned/signed integer types (`UInt16`, `Int16`, `UInt32`, `Int32`) | Python has a single `int` type; readers still exist for protocol correctness | Python integers are arbitrary precision |
| 10 | `namespace CommandMessenger` | `cmd_messenger` package | PEP 8 package naming |
| 11 | `delegate void MessengerCallbackFunction` | `Callable[[ReceivedCommand], None]` type hint | Python uses callable objects directly |
| 12 | `SpinWait.SpinUntil(...)` | `threading.Event.wait()` or busy-wait with `time.sleep` | More efficient Python equivalent |
| 13 | `SerialPort` (.NET) | `serial.Serial` (pyserial) | Platform library replacement |
| 14 | `TcpClient` (.NET) | `socket.socket` (stdlib) | Platform library replacement |
| 15 | `Encoding.GetEncoding("ISO-8859-1")` | `str.encode('latin-1')` / `bytes.decode('latin-1')` | Python built-in encoding support |
| 16 | Separate NuGet packages per transport (`CommandMessenger.Transport.Serial`) | Sub-packages under `transport/` in one installable package | Simpler PyPI distribution |
| 17 | `ListQueue<T> : List<T>` | `ListQueue` wrapping `collections.deque` | deque is O(1) for both ends |

---

## 6. Naming Convention Translation Rules

| C# | Python | Example |
|----|--------|---------|
| `ClassName` | `ClassName` | `CmdMessenger` → `CmdMessenger` |
| `MethodName()` | `method_name()` | `SendCommand()` → `send_command()` |
| `PropertyName` | `property_name` | `PrintLfCr` → `print_lf_cr` |
| `_privateField` | `_private_field` | `_communicationManager` → `_communication_manager` |
| `CONSTANT` | `CONSTANT` | (same) |
| `IInterface` | `BaseClass` (ABC) | `ITransport` → `Transport` |
| `EventHandler<T>` | `Callable[..., None]` | event → callback attribute |

---

## 7. Dependencies

- **Required:** `pyserial` (for `SerialTransport`)
- **Optional:** none (TCP uses stdlib `socket`)
- **Dev:** `pytest`, `pytest-timeout`

---

## 8. Minimum Python Version

- **Python 3.10+** (for `X | Y` union types, `match` statement potential)

---

## 9. Example Usage (Target API)

```python
from cmd_messenger import CmdMessenger, SendCommand, BoardType
from cmd_messenger.transport.serial import SerialTransport, SerialSettings

# Setup transport
settings = SerialSettings(port_name='COM3', baud_rate=115200)
transport = SerialTransport()
transport.current_serial_settings = settings

# Create messenger
with CmdMessenger(transport, board_type=BoardType.BIT_16) as messenger:
    messenger.connect()

    # Attach callbacks
    def on_identify(received_command):
        name = received_command.read_string_arg()
        print(f"Arduino says: {name}")

    messenger.attach(on_identify, message_id=1)

    # Send command expecting acknowledgement
    cmd = SendCommand(0, ack_cmd_id=1, timeout=1000)
    response = messenger.send_command(cmd)

    if response.ok:
        print(response.read_string_arg())
```

---

## 10. Implementation Priority

1. **Core:** `Command`, `SendCommand`, `ReceivedCommand`, `Escaping`, `BinaryConverter`
2. **Engine:** `CommunicationManager`, `CmdMessenger`, queues
3. **Transport:** `Transport` ABC, `SerialTransport`, `SerialSettings`
4. **Connection management:** `ConnectionManager`, `SerialConnectionManager`
5. **Network:** `TcpTransport`, `TcpConnectionManager`
6. **Extras:** `Logger`, `StaleGeneralStrategy`, `CollapseCommandStrategy`
7. **Samples:** Console examples → Web UI examples (see Section 11)

---

## 11. Host-Side Sample Applications

### 11.1 Approach: Two Tiers

The C# examples use two UI technologies:
- **Console apps** — pure `Console.WriteLine` (Receive, SendAndReceive, SendAndReceiveArguments, SendAndReceiveBinaryArguments, SimpleWatchdog, ConsoleShell)
- **WinForms + ZedGraph** — real-time charts, sliders, buttons (DataLogging, ArduinoController, TemperatureControl)

For Python, we map these to:

| Tier | C# Technology | Python Equivalent | Reason |
|------|--------------|-------------------|--------|
| Console | `Console.WriteLine` + loop | Plain Python `print()` + loop | Direct 1:1 match |
| GUI | WinForms + ZedGraph | **Web UI** (FastAPI + WebSocket + HTML/JS) | Cross-platform, no native deps, real-time charts trivial |

**Why Web UI over desktop frameworks?**
- Works on Raspberry Pi, Linux, Mac, Windows — major CmdMessenger use-case
- No heavy dependency (no Qt/Tk)
- WebSocket provides the same real-time push that `ControlToInvokeOn.BeginInvoke` gives in WinForms
- Charting via Plotly.js or Chart.js is richer than ZedGraph with zero install
- Modern, inspectable, extensible (REST API comes free)
- Aligns with IoT industry trends

### 11.2 Sample Layout

Mirrors the C# structure: each sample has a **logic class** (like `SendAndReceive.cs`) with `setup()` / `loop()` / `exit()`, a **thin entry point** (`program.py` ≈ `Program.cs`), and UI complexity hidden in shared backing modules.

```
extras/
└── Python/
    ├── requirements.txt              # pyserial, fastapi, uvicorn, websockets
    │
    ├── shared/                       # ≈ ConsoleUtils.cs + ChartForm.cs (hidden complexity)
    │   ├── __init__.py
    │   ├── console_utils.py          # Ctrl+C handler, RunLoop helper (≈ ConsoleUtils.cs)
    │   └── web_form.py               # WebForm base class (≈ ChartForm.cs / ControllerForm.cs)
    │                                 #   - starts FastAPI + uvicorn
    │                                 #   - manages WebSocket broadcast
    │                                 #   - serves static/index.html
    │                                 #   - provides set_chart_data(), add_slider(), etc.
    │
    ├── 1_receive/
    │   ├── program.py                # Entry point (≈ Program.cs)
    │   └── receive.py                # Logic class: setup(), loop(), exit()
    │
    ├── 2_send_and_receive/
    │   ├── program.py
    │   └── send_and_receive.py       # Logic class: setup(), loop(), exit()
    │
    ├── 3_send_and_receive_arguments/
    │   ├── program.py
    │   └── send_and_receive_arguments.py
    │
    ├── 4_send_and_receive_binary_arguments/
    │   ├── program.py
    │   └── send_and_receive_binary_arguments.py
    │
    ├── 5_data_logging/
    │   ├── program.py                # Entry point: creates WebForm, passes to DataLogging
    │   ├── data_logging.py           # Logic class: setup(chart_form), exit()
    │   └── static/
    │       └── index.html            # Chart view (≈ ChartForm.Designer.cs)
    │
    ├── 6_arduino_controller/
    │   ├── program.py
    │   ├── arduino_controller.py     # Logic class: setup(controller_form), exit()
    │   └── static/
    │       └── index.html            # Slider + toggle (≈ ControllerForm.Designer.cs)
    │
    ├── 7_simple_watchdog/
    │   ├── program.py
    │   └── simple_watchdog.py        # Logic class: setup(), loop(), exit()
    │
    └── 9_temperature_control/
        ├── program.py
        ├── temperature_control.py    # Logic class: setup(chart_form), exit()
        └── static/
            └── index.html            # Chart + slider (≈ ChartForm.Designer.cs)
```

### 11.3 Structural Pattern — Matching C# Minimalism

The key insight: in C#, `DataLogging.cs` is ~100 lines of pure application logic. All WinForms/ZedGraph complexity is hidden in `ChartForm.cs`. We replicate this exactly.

#### C# Structure:
```
Program.cs          → var dl = new DataLogging(); dl.Setup(new ChartForm());
DataLogging.cs      → setup(), callbacks, exit()  [CLEAN - user reads this]
ChartForm.cs        → WinForms + ZedGraph wiring  [HIDDEN - user ignores this]
```

#### Python Structure:
```
program.py          → dl = DataLogging(); dl.setup(WebForm(...))
data_logging.py     → setup(), callbacks, exit()  [CLEAN - user reads this]
shared/web_form.py  → FastAPI + WebSocket wiring  [HIDDEN - user ignores this]
static/index.html   → Plotly.js chart layout      [HIDDEN - user ignores this]
```

#### The `shared/web_form.py` — WebForm class (≈ ChartForm base)

```python
class WebForm:
    """Base class hiding all web/WebSocket complexity — equivalent of ChartForm.
    
    User never needs to look inside; they just call methods like set_chart_data().
    """

    def __init__(self, title: str = "CmdMessenger", port: int = 8080,
                 static_dir: str = "static"):
        self._app: FastAPI
        self._broadcaster: _Broadcaster
        self._server_thread: Thread

    # --- Public API (what the logic class calls) ---
    def start(self) -> None: ...              # Start web server in background thread
    def stop(self) -> None: ...               # Shutdown server
    def send_to_clients(self, data: dict) -> None: ...  # Push data via WebSocket
    def on_command(self, name: str, handler: Callable) -> None:  # Register REST endpoint

    # --- Convenience helpers matching C# ChartForm patterns ---
    def setup_chart(self, title: str, x_label: str, y_label: str,
                    series: list[str]) -> None: ...
    def update_chart(self, **series_data) -> None: ...
```

#### The `shared/console_utils.py` (≈ ConsoleUtils.cs)

```python
class ConsoleUtils:
    """Handles Ctrl+C gracefully — mirrors C# ConsoleUtils."""
    on_close: Callable[[], None] | None = None

    @staticmethod
    def run_loop(logic, setup_args=None):
        """Run a logic class with setup()/loop()/exit() lifecycle."""
        ...
```

### 11.4 What the User Reads — Side-by-Side

#### C# `DataLogging.cs` (simplified):
```csharp
public class DataLogging {
    private CmdMessenger _cmdMessenger;
    private ChartForm _chartForm;

    public void Setup(ChartForm chartForm) {
        _chartForm = chartForm;
        _chartForm.SetupChart();
        _serialTransport = new SerialTransport { ... };
        _cmdMessenger = new CmdMessenger(_serialTransport, BoardType.Bit16);
        _cmdMessenger.Attach((int)Command.PlotDataPoint, OnPlotDataPoint);
        _cmdMessenger.Connect();
        _cmdMessenger.SendCommand(new SendCommand((int)Command.StartLogging));
    }

    void OnPlotDataPoint(ReceivedCommand cmd) {
        var time = cmd.ReadFloatArg();
        var val1 = cmd.ReadFloatArg();
        _chartForm.UpdateChart(time, val1);
    }

    public void Exit() {
        _cmdMessenger.Disconnect();
        _cmdMessenger.Dispose();
    }
}
```

#### Python `data_logging.py` (same structure, same density):
```python
class DataLogging:
    def __init__(self):
        self._transport: SerialTransport
        self._messenger: CmdMessenger
        self._chart_form: WebForm

    def setup(self, chart_form: WebForm):
        self._chart_form = chart_form
        self._chart_form.setup_chart(title="Data Logging", x_label="Time (s)",
                                     y_label="Voltage (V)", series=["Analog 1", "Analog 2"])

        self._transport = SerialTransport()
        self._transport.current_serial_settings = SerialSettings(port_name='COM6', baud_rate=115200)

        self._messenger = CmdMessenger(self._transport, board_type=BoardType.BIT_16)
        self._messenger.attach(self._on_plot_data_point, message_id=Command.PLOT_DATA_POINT)
        self._messenger.connect()
        self._messenger.send_command(SendCommand(Command.START_LOGGING))

    def _on_plot_data_point(self, cmd: ReceivedCommand):
        t = cmd.read_float_arg()
        v1 = cmd.read_float_arg()
        v2 = cmd.read_float_arg()
        self._chart_form.update_chart(time=t, analog_1=v1, analog_2=v2)

    def exit(self):
        self._messenger.disconnect()
        self._messenger.dispose()
```

#### Python `program.py` (≈ Program.cs):
```python
from shared import WebForm, ConsoleUtils
from data_logging import DataLogging

def main():
    form = WebForm(title="Data Logging", static_dir="static")
    app = DataLogging()
    ConsoleUtils.on_close = app.exit
    app.setup(form)
    form.start()       # blocks until Ctrl+C or window close

if __name__ == "__main__":
    main()
```

### 11.5 Web UI Architecture

```
┌──────────────────────────────────────────────────────────┐
│  Browser (index.html)                                    │
│  ┌────────────────┐  ┌────────────────────────────────┐  │
│  │ Controls       │  │ Plotly.js / Chart.js           │  │
│  │ (slider, btn)  │  │ (real-time rolling chart)      │  │
│  └───────┬────────┘  └──────────────▲─────────────────┘  │
│          │ POST /api/set_xxx         │ ws://host/ws       │
└──────────┼───────────────────────────┼────────────────────┘
           │                           │
┌──────────▼───────────────────────────┼────────────────────┐
│  FastAPI Backend (e.g. data_logging.py)                    │
│  ┌──────────────────┐  ┌────────────┴──────────────────┐  │
│  │ REST endpoints   │  │ WebSocket manager             │  │
│  │ (commands in)    │  │ (broadcasts data to clients)  │  │
│  └────────┬─────────┘  └──────────────▲───────────────┘  │
│           │                            │                   │
│  ┌────────▼────────────────────────────┴───────────────┐  │
│  │  Application Logic (= C# DataLogging class equiv.)  │  │
│  │  - attach() callbacks                               │  │
│  │  - on_plot_data_point → push to WebSocket           │  │
│  │  - on_slider_change → send_command(SetGoalTemp)     │  │
│  └────────────────────────┬────────────────────────────┘  │
│                           │                                │
│  ┌────────────────────────▼────────────────────────────┐  │
│  │  cmd_messenger library                          │  │
│  │  CmdMessenger → CommunicationManager → Transport   │  │
│  └────────────────────────┬────────────────────────────┘  │
└───────────────────────────┼────────────────────────────────┘
                            │ Serial / TCP
                    ┌───────▼───────┐
                    │   Arduino     │
                    └───────────────┘
```

**Key mapping: WinForms → Web**

| WinForms concept | Web equivalent |
|-----------------|----------------|
| `ControlToInvokeOn` / `BeginInvoke` | WebSocket broadcast (thread-safe via `asyncio`) |
| `Timer` + chart redraw | Client-side `requestAnimationFrame` + Plotly `extendTraces` |
| `TrackBar.ValueChanged` event | `<input type="range">` → `POST /api/set_frequency` |
| `CheckBox.CheckedChanged` | `<button>` toggle → `POST /api/set_led` |
| ZedGraph `RollingPointPairList` | Plotly.js with rolling window (client-side array trim) |
| `Form.Close` → `Exit()` | FastAPI `on_event("shutdown")` → `messenger.dispose()` |

### 11.6 Console Example — Side-by-Side

#### C# `Program.cs`:
```csharp
var sendAndReceive = new SendAndReceive { RunLoop = true };
ConsoleUtils.ConsoleClose += (o, i) => sendAndReceive.Exit();
sendAndReceive.Setup();
while (sendAndReceive.RunLoop) sendAndReceive.Loop();
sendAndReceive.Exit();
```

#### Python `program.py`:
```python
from shared import ConsoleUtils
from send_and_receive import SendAndReceive

def main():
    app = SendAndReceive()
    ConsoleUtils.on_close = app.exit
    app.setup()
    ConsoleUtils.run_loop(app)

if __name__ == "__main__":
    main()
```

#### Python `send_and_receive.py` (logic class):
```python
class SendAndReceive:
    def __init__(self):
        self.run_loop = True
        self._led_state = False
        self._count = 0

    def setup(self):
        self._transport = SerialTransport()
        self._transport.current_serial_settings = SerialSettings(port_name='COM6', baud_rate=115200)
        self._messenger = CmdMessenger(self._transport, board_type=BoardType.BIT_16)
        self._attach_callbacks()
        self._messenger.connect()

    def loop(self):
        self._count += 1
        self._messenger.send_command(SendCommand(Command.SET_LED, self._led_state))
        time.sleep(1.0)
        self._led_state = not self._led_state
        if self._count > 100:
            self.run_loop = False

    def exit(self):
        self._messenger.disconnect()
        self._messenger.dispose()
        self._transport.dispose()

    def _attach_callbacks(self):
        self._messenger.attach(self._on_unknown)
        self._messenger.attach(self._on_status, message_id=Command.STATUS)

    def _on_unknown(self, cmd: ReceivedCommand):
        print(f"Command without callback: {cmd.cmd_id}")

    def _on_status(self, cmd: ReceivedCommand):
        print(f"LED status: {cmd.read_bool_arg()}")
```

### 11.7 Dependencies for Samples

```
# requirements.txt (extras/Python/)
cmd_messenger          # the library itself (local or pip)
pyserial>=3.5
fastapi>=0.100
uvicorn[standard]>=0.20
websockets>=11.0
```

Console-only samples need only `pyserial`. Web UI samples add `fastapi` + `uvicorn`.

### 11.8 Mapping C# Samples → Python Samples

| # | C# Sample | Python | UI Type | Key Features Demonstrated |
|---|-----------|--------|---------|---------------------------|
| 1 | Receive | `1_receive/` | Console | Basic attach + listen |
| 2 | SendAndReceive | `2_send_and_receive/` | Console | Send command, receive callback |
| 3 | SendAndReceiveArguments | `3_send_and_receive_arguments/` | Console | Typed arguments (float) |
| 4 | SendAndReceiveBinaryArguments | `4_send_and_receive_binary_arguments/` | Console | Binary arg encoding |
| 5 | DataLogging | `5_data_logging/` | Web | Real-time chart, StaleGeneralStrategy |
| 6 | ArduinoController | `6_arduino_controller/` | Web | Slider, CollapseCommandStrategy |
| 7 | SimpleWatchdog | `7_simple_watchdog/` | Console | SerialConnectionManager, auto-reconnect |
| 9 | TemperatureControl | `9_temperature_control/` | Web | Full: chart + slider + ConnectionManager + watchdog |
