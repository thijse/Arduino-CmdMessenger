"""Subprocess transport for native loopback firmware integration tests."""
from __future__ import annotations

import subprocess
import threading
from collections import deque

from cmd_messenger.transport.transport import Transport


class FirmwareProcessTransport(Transport):
    """Transport that talks to native loopback firmware over stdin/stdout pipes."""

    def __init__(self, executable_path: str) -> None:
        super().__init__()
        self._executable_path = executable_path
        self._process: subprocess.Popen[bytes] | None = None
        self._buffer: deque[bytes] = deque()
        self._lock = threading.Lock()
        self._running = False
        self._reader_thread: threading.Thread | None = None

    def connect(self) -> bool:
        self._process = subprocess.Popen(
            [self._executable_path],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, "CREATE_NO_WINDOW") else 0,
        )
        self._running = True
        self._reader_thread = threading.Thread(
            target=self._reader_loop,
            name="FirmwareStdoutReader",
            daemon=True,
        )
        self._reader_thread.start()
        return True

    def disconnect(self) -> bool:
        self._running = False
        process = self._process
        if process is None:
            return True
        try:
            if process.stdin is not None:
                process.stdin.close()
        except Exception:
            pass
        try:
            process.wait(timeout=2.0)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=2.0)
        if self._reader_thread is not None:
            self._reader_thread.join(timeout=0.5)
        return True

    def is_connected(self) -> bool:
        return self._process is not None and self._process.poll() is None

    def read(self) -> bytes:
        with self._lock:
            if self._buffer:
                return self._buffer.popleft()
        return b""

    def write(self, data: bytes) -> None:
        process = self._process
        if not data or process is None or process.stdin is None:
            return
        process.stdin.write(data)
        process.stdin.flush()

    def dispose(self) -> None:
        self.disconnect()

    def _reader_loop(self) -> None:
        process = self._process
        if process is None or process.stdout is None:
            return
        while self._running and process.poll() is None:
            chunk = process.stdout.read(1)
            if not chunk:
                break
            with self._lock:
                self._buffer.append(chunk)
            self.data_received(self)
