"""In-memory LoopbackTransport for unit testing.

Port of C# ``LoopbackTransport.cs``. Uses a ``collections.deque`` as a
byte buffer. :meth:`simulate_receive` injects data as if it arrived from
the embedded side (fires :attr:`data_received`).
"""
from __future__ import annotations

import threading
from collections import deque

from cmd_messenger.transport.transport import Transport


class LoopbackTransport(Transport):
    """In-memory transport stub for host-side tests — no serial port needed."""

    def __init__(self) -> None:
        super().__init__()
        self._buffer: deque[bytes] = deque()
        self._lock = threading.Lock()
        self._connected = False

    def connect(self) -> bool:
        self._connected = True
        return True

    def disconnect(self) -> bool:
        self._connected = False
        return True

    def is_connected(self) -> bool:
        return self._connected

    def read(self) -> bytes:
        with self._lock:
            if self._buffer:
                return self._buffer.popleft()
        return b""

    def write(self, data: bytes) -> None:
        if not data:
            return
        with self._lock:
            self._buffer.append(bytes(data))
        self.data_received(self)

    def simulate_receive(self, data: bytes) -> None:
        """Inject bytes as if they arrived from the remote side."""
        if not data:
            return
        with self._lock:
            self._buffer.append(bytes(data))
        self.data_received(self)

    def dispose(self) -> None:
        self._connected = False
