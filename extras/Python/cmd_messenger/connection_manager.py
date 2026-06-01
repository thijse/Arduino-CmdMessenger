"""``ConnectionManager`` — port of C# ``ConnectionManager``.

Abstract base for transport-specific connection managers. Owns an
:class:`AsyncWorker` that switches between scan / connect / watchdog modes
and fires :data:`connection_found` / :data:`connection_timeout` events.
"""
from __future__ import annotations

import enum
import threading
import time
from abc import ABC, abstractmethod
from typing import Optional

from . import time_utils
from .async_worker import AsyncWorker
from .cmd_messenger import CmdMessenger
from .enums import ReceiveQueue, SendQueue, UseQueue
from .event import Event
from .received_command import ReceivedCommand
from .send_command import SendCommand


class Mode(enum.Enum):
    WAIT = "wait"
    CONNECT = "connect"
    SCAN = "scan"
    WATCHDOG = "watchdog"


class DeviceStatus(enum.Enum):
    NOT_AVAILABLE = "not_available"
    AVAILABLE = "available"
    IDENTITY_MISMATCH = "identity_mismatch"


class ConnectionManagerProgressEventArgs:
    def __init__(self, level: int, description: str) -> None:
        self.level = level
        self.description = description

    def __repr__(self) -> str:
        return f"<Progress level={self.level} {self.description!r}>"


class ConnectionManager(ABC):
    """Abstract connection manager."""

    def __init__(
        self,
        cmd_messenger: CmdMessenger,
        identify_command_id: int = 0,
        unique_device_id: Optional[str] = None,
    ) -> None:
        if cmd_messenger is None:
            raise ValueError("Command Messenger is null.")

        #: Fires () when the device fails to respond.
        self.connection_timeout: Event = Event()
        #: Fires () when a device responds successfully.
        self.connection_found: Event = Event()
        #: Fires (ConnectionManagerProgressEventArgs) for log messages.
        self.progress: Event = Event()

        self._cmd_messenger = cmd_messenger
        self._identify_command_id = identify_command_id
        self._unique_device_id = unique_device_id

        self.watchdog_timeout: int = 3000
        self.watchdog_retry_timeout: int = 1500
        self.watchdog_tries: int = 3
        self._watchdog_enabled: bool = False

        #: Whether to persist last-good settings (subclass-specific).
        self.persistent_settings: bool = False
        #: Whether to scan for devices vs connect to a known device.
        self.device_scan_enabled: bool = True

        #: Currently connected to a device.
        self.connected: bool = False

        self._mode: Mode = Mode.WAIT
        self._last_check_time: int = 0
        self._next_time_out_check: int = 0
        self._watchdog_tries_used: int = 0

        self._worker = AsyncWorker(self._do_work, name="ConnectionManager")

        if unique_device_id:
            cmd_messenger.attach(identify_command_id, self._on_identify_response)

    # ------------------------------------------------------------------
    # Watchdog property
    # ------------------------------------------------------------------
    @property
    def watchdog_enabled(self) -> bool:
        return self._watchdog_enabled

    @watchdog_enabled.setter
    def watchdog_enabled(self, value: bool) -> None:
        if value and not self._unique_device_id:
            raise RuntimeError("Watchdog can't be enabled without Unique Device ID.")
        self._watchdog_enabled = value

    @property
    def mode(self) -> Mode:
        return self._mode

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------
    def start_connection_manager(self) -> None:
        if not self._worker.is_running:
            self._worker.start()
        if self.device_scan_enabled:
            self._start_scan()
        else:
            self._start_connect()

    def stop_connection_manager(self) -> None:
        if self._worker.is_running:
            self._worker.stop()
        self._disconnect()

    def dispose(self) -> None:
        self.stop_connection_manager()

    def __enter__(self) -> "ConnectionManager":
        return self

    def __exit__(self, *exc_info) -> None:
        self.dispose()

    # ------------------------------------------------------------------
    # Events
    # ------------------------------------------------------------------
    def _connection_found_event(self) -> None:
        self._mode = Mode.WAIT
        if self._watchdog_enabled:
            self._start_watchdog()
        try:
            self.connection_found()
        except Exception:
            pass

    def _connection_timeout_event(self) -> None:
        self._mode = Mode.WAIT
        self._disconnect()
        try:
            self.connection_timeout()
        except Exception:
            pass
        if self._watchdog_enabled:
            self._stop_watchdog()
            if self.device_scan_enabled:
                self._start_scan()
            else:
                self._start_connect()

    def _log(self, level: int, message: str) -> None:
        try:
            self.progress(ConnectionManagerProgressEventArgs(level, message))
        except Exception:
            pass

    # ------------------------------------------------------------------
    # Identify / watchdog
    # ------------------------------------------------------------------
    def _on_identify_response(self, response_command: ReceivedCommand) -> None:
        if response_command.ok and self._unique_device_id:
            self._validate_device_unique_id(response_command)

    def _arduino_available(self, timeout_ms: int, tries: int = 1) -> DeviceStatus:
        for i in range(1, tries + 1):
            if tries > 1:
                self._log(3, f"Polling Arduino, try # {i}")
            challenge = SendCommand(
                self._identify_command_id,
                self._identify_command_id,
                ack_cmd_id=self._identify_command_id,
                timeout=timeout_ms,
            )
            response = self._cmd_messenger.send_command(
                challenge,
                SendQueue.IN_FRONT_QUEUE,
                ReceiveQueue.DEFAULT,
                UseQueue.BYPASS_QUEUE,
            )
            if response.ok and self._unique_device_id:
                return (
                    DeviceStatus.AVAILABLE
                    if self._validate_device_unique_id(response)
                    else DeviceStatus.IDENTITY_MISMATCH
                )
            if response.ok:
                return DeviceStatus.AVAILABLE
        return DeviceStatus.NOT_AVAILABLE

    def _validate_device_unique_id(self, response_command: ReceivedCommand) -> bool:
        valid = self._unique_device_id == response_command.read_string_arg()
        if not valid:
            self._log(3, "Invalid device response. Device ID mismatch.")
        return valid

    # ------------------------------------------------------------------
    # Worker job
    # ------------------------------------------------------------------
    def _do_work(self) -> bool:
        if self._mode == Mode.SCAN:
            self._do_work_scan()
        elif self._mode == Mode.CONNECT:
            self._do_work_connect()
        elif self._mode == Mode.WATCHDOG:
            self._do_work_watchdog()
        time.sleep(0.1)
        return True

    @abstractmethod
    def _do_work_connect(self) -> None:
        """Subclass: try to connect using current settings."""

    @abstractmethod
    def _do_work_scan(self) -> None:
        """Subclass: scan available devices for a match."""

    def _do_work_watchdog(self) -> None:
        last_line_time_stamp = self._cmd_messenger.last_received_command_time_stamp
        current = time_utils.millis()
        if current < self._next_time_out_check:
            return
        if last_line_time_stamp >= self._last_check_time:
            self._log(3, "Successful watchdog response.")
            self._last_check_time = current
            self._next_time_out_check = self._last_check_time + self.watchdog_timeout
            self._watchdog_tries_used = 0
            return

        if self._watchdog_tries_used >= self.watchdog_tries:
            self._log(2, f"Watchdog received no response after final try #{self.watchdog_tries}")
            self._watchdog_tries_used = 0
            self._mode = Mode.WAIT
            self._connection_timeout_event()
            return

        self._cmd_messenger.send_command(SendCommand(self._identify_command_id))
        self._watchdog_tries_used += 1
        self._last_check_time = current
        self._next_time_out_check = self._last_check_time + self.watchdog_retry_timeout
        if self._watchdog_tries_used == 1:
            self._log(
                3,
                f"Watchdog detected no communication for {self.watchdog_timeout / 1000.0}s, asking for response",
            )
        else:
            self._log(3, f"Watchdog received no response, performing try #{self._watchdog_tries_used}")

    # ------------------------------------------------------------------
    # State transitions
    # ------------------------------------------------------------------
    def _disconnect(self) -> bool:
        if self.connected:
            self.connected = False
            return self._cmd_messenger.disconnect()
        return True

    def _start_watchdog(self) -> None:
        if self._mode != Mode.WATCHDOG and self.connected:
            self._log(1, "Starting Watchdog.")
            self._last_check_time = time_utils.millis()
            self._next_time_out_check = self._last_check_time + self.watchdog_timeout
            self._watchdog_tries_used = 0
            self._mode = Mode.WATCHDOG

    def _stop_watchdog(self) -> None:
        if self._mode == Mode.WATCHDOG:
            self._log(1, "Stopping Watchdog.")
            self._mode = Mode.WAIT

    def _start_scan(self) -> None:
        if self._mode != Mode.SCAN and not self.connected:
            self._log(1, "Starting device scan.")
            self._mode = Mode.SCAN

    def _stop_scan(self) -> None:
        if self._mode == Mode.SCAN:
            self._log(1, "Stopping device scan.")
            self._mode = Mode.WAIT

    def _start_connect(self) -> None:
        if self._mode != Mode.CONNECT and not self.connected:
            self._log(1, "Start connecting to device.")
            self._mode = Mode.CONNECT

    def _stop_connect(self) -> None:
        if self._mode == Mode.CONNECT:
            self._log(1, "Stop connecting to device.")
            self._mode = Mode.WAIT

    # ------------------------------------------------------------------
    # Persistence (subclass-specific)
    # ------------------------------------------------------------------
    def _store_settings(self) -> None:  # pragma: no cover - hook
        pass

    def _read_settings(self) -> None:  # pragma: no cover - hook
        pass
