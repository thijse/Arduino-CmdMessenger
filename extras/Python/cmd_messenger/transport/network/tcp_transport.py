"""``TcpTransport`` — port of C# ``TcpTransport``.

Background-thread polling reader, modeled after :class:`SerialTransport`.
"""
from __future__ import annotations

import socket
import threading
from typing import Optional

from ...async_worker import AsyncWorker
from ..transport import Transport

_BUFFER_SIZE = 4096


class TcpTransport(Transport):
    """TCP-client transport."""

    def __init__(self, host: str, port: int, timeout_ms: int = 1000) -> None:
        super().__init__()
        self.host = host
        self.port = port
        self.timeout: int = timeout_ms  # milliseconds, matches C# property name

        self._socket: Optional[socket.socket] = None
        self._buffer = bytearray()
        self._read_lock = threading.Lock()
        self._sock_lock = threading.Lock()
        self._worker = AsyncWorker(self._poll, name="TcpTransport")

    # ------------------------------------------------------------------
    # Transport contract
    # ------------------------------------------------------------------
    def connect(self) -> bool:
        if self.is_connected():
            raise RuntimeError("Already connected.")
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(self.timeout / 1000.0 if self.timeout > 0 else None)
            sock.connect((self.host, self.port))
            # Use a short blocking timeout for the poll loop so we can stop quickly.
            sock.settimeout(0.1)
        except OSError:
            return False
        self._socket = sock
        with self._read_lock:
            self._buffer = bytearray()
        self._worker.start()
        return True

    def disconnect(self) -> bool:
        if self._worker.is_running:
            self._worker.stop()
        with self._sock_lock:
            sock = self._socket
            self._socket = None
        if sock is not None:
            try:
                sock.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            try:
                sock.close()
            except OSError:
                pass
        return True

    def is_connected(self) -> bool:
        return self._socket is not None

    def read(self) -> bytes:
        with self._read_lock:
            data = bytes(self._buffer)
            self._buffer = bytearray()
        return data

    def write(self, buffer: bytes) -> None:
        if not self.is_connected():
            return
        with self._sock_lock:
            sock = self._socket
        if sock is None:
            return
        try:
            sock.sendall(buffer)
        except OSError:
            self.disconnect()

    # ------------------------------------------------------------------
    # Worker job
    # ------------------------------------------------------------------
    def _poll(self) -> bool:
        with self._sock_lock:
            sock = self._socket
        if sock is None:
            return False
        try:
            chunk = sock.recv(_BUFFER_SIZE)
        except socket.timeout:
            return True
        except OSError:
            return False
        if not chunk:
            # Peer closed.
            self.disconnect()
            return False
        with self._read_lock:
            self._buffer.extend(chunk)
        try:
            self.data_received()
        except Exception:
            pass
        return True
