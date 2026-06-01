"""``Transport`` abstract base — port of C# ``ITransport``.

Implementations must:

* Manage their own connection lifecycle (:meth:`connect`/:meth:`disconnect`).
* Buffer incoming bytes and expose them via :meth:`read` (returns *all*
  bytes currently buffered, then clears the buffer).
* Fire :attr:`data_received` whenever new bytes arrive — this is what wakes
  the receive pipeline.

The :attr:`data_received` slot is an :class:`~cmd_messenger.event.Event` so it
supports the C#-style ``transport.data_received += handler`` pattern.
"""
from __future__ import annotations

from abc import ABC, abstractmethod

from ..event import Event


class Transport(ABC):
    """Abstract transport layer (``ITransport`` in C#)."""

    def __init__(self) -> None:
        #: Fired (no args) whenever new bytes are available.
        self.data_received: Event = Event()

    @abstractmethod
    def connect(self) -> bool:
        """Open the underlying connection. Returns ``True`` on success."""

    @abstractmethod
    def disconnect(self) -> bool:
        """Close the underlying connection. Returns ``True`` on success."""

    @abstractmethod
    def is_connected(self) -> bool:
        """Return whether the connection is currently open."""

    @abstractmethod
    def read(self) -> bytes:
        """Return all bytes currently buffered. May return ``b''``."""

    @abstractmethod
    def write(self, data: bytes) -> None:
        """Send raw bytes."""

    # Dispose / context-manager support (mirrors C# IDisposable)
    def dispose(self) -> None:
        try:
            self.disconnect()
        except Exception:
            pass

    def __enter__(self) -> "Transport":
        self.connect()
        return self

    def __exit__(self, *exc_info) -> None:
        self.dispose()
