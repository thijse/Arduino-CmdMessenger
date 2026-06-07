"""Py-CmdMessenger — Pythonic port of the C#/VB CommandMessenger library.

Pythonic Arduino serial communication mirroring the architecture of the
parent C# library (CommandMessenger namespace).

See ``plans/Python/architecture.md`` for the full design.
"""

__version__ = "0.1.0"

# Core primitives (Layer 2)
from . import binary_converter, escaping, time_utils
from .event import Event

# Command model (Layer 3)
from .command import Command
from .enums import BoardType, ReceiveQueue, SendQueue, UseQueue
from .received_command import ReceivedCommand
from .send_command import SendCommand

# Synchronization primitives (Layer 4)
from .async_worker import AsyncWorker, WorkerState
from .event_waiter import EventWaiter, WaitState
from .received_command_signal import ReceivedCommandSignal

# Transport layer (Layer 5)
from .transport import Transport
from .transport.serial import Parity, SerialSettings, SerialTransport, StopBits

# Queue layer (Layer 6)
from .queue import (
    CollapseCommandStrategy,
    CommandQueue,
    CommandStrategy,
    GeneralStrategy,
    ListQueue,
    ReceiveCommandQueue,
    SendCommandQueue,
    StaleGeneralStrategy,
    TopCommandStrategy,
)

# Layer 7 — façade
from .communication_manager import CommunicationManager
from .cmd_messenger import CmdMessenger, MessengerCallbackFunction

# Layer 8 — connection manager
from . import logger
from .connection_manager import (
    ConnectionManager,
    ConnectionManagerProgressEventArgs,
    DeviceStatus,
    Mode,
)
from .connection_storer import JsonSerialConnectionStorer, SerialConnectionStorer
from .transport.serial import (
    SerialConnectionManager,
    SerialConnectionManagerSettings,
)

# Layer 9 — network transport
from .transport.network import TcpConnectionManager, TcpTransport

__all__ = [
    "__version__",
    "Event",
    "escaping",
    "binary_converter",
    "time_utils",
    "Command",
    "SendCommand",
    "ReceivedCommand",
    "BoardType",
    "SendQueue",
    "ReceiveQueue",
    "UseQueue",
    "EventWaiter",
    "WaitState",
    "AsyncWorker",
    "WorkerState",
    "ReceivedCommandSignal",
    "Transport",
    "SerialTransport",
    "SerialSettings",
    "Parity",
    "StopBits",
    "CommandQueue",
    "CommandStrategy",
    "CollapseCommandStrategy",
    "TopCommandStrategy",
    "GeneralStrategy",
    "StaleGeneralStrategy",
    "ListQueue",
    "SendCommandQueue",
    "ReceiveCommandQueue",
    "CommunicationManager",
    "CmdMessenger",
    "MessengerCallbackFunction",
    "logger",
    "ConnectionManager",
    "ConnectionManagerProgressEventArgs",
    "DeviceStatus",
    "Mode",
    "SerialConnectionManager",
    "SerialConnectionManagerSettings",
    "SerialConnectionStorer",
    "JsonSerialConnectionStorer",
    "TcpTransport",
    "TcpConnectionManager",
]
