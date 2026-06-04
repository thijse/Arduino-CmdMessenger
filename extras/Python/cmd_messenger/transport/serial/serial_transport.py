"""``SerialTransport`` — port of C# ``SerialTransport``.

Architecture mirrors C# 1:1:
* An :class:`AsyncWorker` polls the serial port on a background thread.
* Each poll attempts to read bytes (timeout-driven, no busy spin); newly read
  bytes are appended to an internal buffer and :attr:`data_received` fires.
* :meth:`read` returns and clears the buffer (matches C# semantics).
"""
from __future__ import annotations

import threading
from typing import Optional

import serial as _pyserial

from ...async_worker import AsyncWorker
from ..transport import Transport
from .serial_settings import SerialSettings

_BUFFER_SIZE = 4096


class SerialTransport(Transport):
    """Threaded serial-port transport."""

    def __init__(self, settings: Optional[SerialSettings] = None) -> None:
        super().__init__()
        self._connected = False
        self._serial: Optional[_pyserial.Serial] = None
        self._current_serial_settings: SerialSettings = settings or SerialSettings()

        self._read_buffer = bytearray()
        self._read_lock = threading.Lock()
        self._serial_rw_lock = threading.Lock()

        self._worker = AsyncWorker(self._poll, name="SerialTransport")

    # ------------------------------------------------------------------
    # Settings accessor (C# CurrentSerialSettings)
    # ------------------------------------------------------------------
    @property
    def current_serial_settings(self) -> SerialSettings:
        return self._current_serial_settings

    @current_serial_settings.setter
    def current_serial_settings(self, value: SerialSettings) -> None:
        self._current_serial_settings = value

    # ------------------------------------------------------------------
    # Transport API
    # ------------------------------------------------------------------
    def connect(self) -> bool:
        if not self._current_serial_settings.is_valid():
            raise RuntimeError("Unable to open connection - serial settings invalid.")
        if self.is_connected():
            raise RuntimeError("Serial port is already opened.")

        s = self._current_serial_settings
        try:
            # Support pyserial URLs (loop://, socket://, hwgrep://, rfc2217://...)
            # whenever the port name contains '://'. This is a Pythonic extension
            # over C# — useful for tests and TCP-bridged serial servers.
            if "://" in s.port_name:
                self._serial = _pyserial.serial_for_url(
                    s.port_name,
                    baudrate=s.baud_rate,
                    parity=s.parity.value,
                    bytesize=s.data_bits,
                    stopbits=s.stop_bits.value,
                    timeout=1.0,
                    write_timeout=s.timeout / 1000.0,
                )
            else:
                self._serial = _pyserial.Serial(
                    port=s.port_name,
                    baudrate=s.baud_rate,
                    parity=s.parity.value,
                    bytesize=s.data_bits,
                    stopbits=s.stop_bits.value,
                    # Read timeout drives the poll loop (matches C# ReadTimeout=1000).
                    timeout=1.0,
                    write_timeout=s.timeout / 1000.0,
                )
            try:
                self._serial.dtr = s.dtr_enable
            except Exception:
                # Some pyserial URL adapters (e.g. loop://) don't support DTR.
                pass
            try:
                self._serial.rts = s.rts_enable
            except Exception:
                # Some pyserial URL adapters (e.g. loop://) don't support RTS.
                pass
            # Flush any stale data.
            try:
                self._serial.reset_input_buffer()
                self._serial.reset_output_buffer()
            except Exception:
                pass
            self._connected = True
        except Exception:
            self._connected = False
            self._serial = None
            return False

        self._worker.start()
        return True

    def disconnect(self) -> bool:
        result = self._close()
        if self._connected:
            self._connected = False
            try:
                self._worker.stop()
            except RuntimeError:
                # Already stopped — match C# best-effort behaviour.
                pass
        return result

    def is_connected(self) -> bool:
        return self._connected and self._serial is not None and self._serial.is_open

    def write(self, data: bytes) -> None:
        if not self.is_connected():
            return
        try:
            with self._serial_rw_lock:
                assert self._serial is not None
                self._serial.write(data)
        except _pyserial.SerialTimeoutException:
            # Expected — match C# behaviour.
            pass
        except Exception:
            self.disconnect()

    def read(self) -> bytes:
        if not self.is_connected():
            return b""
        with self._read_lock:
            buf = bytes(self._read_buffer)
            self._read_buffer.clear()
        return buf

    # ------------------------------------------------------------------
    # Worker loop
    # ------------------------------------------------------------------
    def _poll(self) -> bool:
        had_bytes = self._update_buffer()
        if had_bytes > 0:
            try:
                self.data_received()
            except Exception:
                # Don't let a misbehaving handler take down the worker.
                pass
        # Always have more work — pacing comes from the serial read timeout.
        return True

    def _update_buffer(self) -> int:
        if not self.is_connected():
            return 0
        try:
            assert self._serial is not None
            # in_waiting can be 0 — fall back to a blocking read of 1 byte
            # (timeout=1.0s on the port object) so the loop doesn't busy spin.
            n = self._serial.in_waiting
            if n <= 0:
                with self._serial_rw_lock:
                    chunk = self._serial.read(1)
            else:
                with self._serial_rw_lock:
                    chunk = self._serial.read(min(n, _BUFFER_SIZE - len(self._read_buffer)))
            if not chunk:
                return 0
            with self._read_lock:
                self._read_buffer.extend(chunk)
                return len(self._read_buffer)
        except _pyserial.SerialTimeoutException:
            return 0
        except Exception:
            self.disconnect()
            return 0

    def _close(self) -> bool:
        if not self.is_connected():
            return False
        try:
            assert self._serial is not None
            self._serial.close()
            return True
        except Exception:
            return False
