"""``SerialConnectionManager`` — port of C# ``SerialConnectionManager``."""
from __future__ import annotations

import enum
import threading
import time
from typing import Callable, List, Optional

from ...cmd_messenger import CmdMessenger
from ...connection_manager import ConnectionManager, DeviceStatus, Mode
from ...connection_storer import ConnectionStorer
from . import serial_utils
from .serial_transport import SerialTransport


class _ScanType(enum.Enum):
    NONE = 0
    QUICK = 1
    THOROUGH = 2


class SerialConnectionManagerSettings:
    """Stored connection settings."""

    def __init__(self, port: str = "", baud_rate: int = 0) -> None:
        self.port = port
        self.baud_rate = baud_rate


class SerialConnectionManager(ConnectionManager):
    """Connection manager for serial-port transports."""

    def __init__(
        self,
        serial_transport: SerialTransport,
        cmd_messenger: CmdMessenger,
        watchdog_command_id: int = 0,
        unique_device_id: Optional[str] = None,
        settings_store: Optional[Callable[[SerialConnectionManagerSettings], None]] = None,
        settings_load: Optional[Callable[[], SerialConnectionManagerSettings]] = None,
        connection_storer: Optional[ConnectionStorer] = None,
    ) -> None:
        super().__init__(cmd_messenger, watchdog_command_id, unique_device_id)
        if serial_transport is None:
            raise ValueError("Transport is null.")

        self._serial_transport = serial_transport
        self._settings_store = settings_store
        self._settings_load = settings_load
        self._connection_storer = connection_storer
        self.persistent_settings = (
            connection_storer is not None
            or (settings_store is not None and settings_load is not None)
        )
        #: Try alternative baud rates during scan.
        self.device_scan_baud_rate_selection: bool = True

        self._try_lock = threading.Lock()
        self._scan_type: _ScanType = _ScanType.NONE
        self.available_serial_ports: List[str] = []
        self._update_available_ports()

        self._stored_settings = SerialConnectionManagerSettings()
        self._read_settings()

    # ------------------------------------------------------------------
    # Connection probing
    # ------------------------------------------------------------------
    def _try_connection(
        self,
        port_name: Optional[str] = None,
        baud_rate: Optional[int] = None,
    ) -> DeviceStatus:
        with self._try_lock:
            settings = self._serial_transport.current_serial_settings
            old_port = settings.port_name
            old_baud = settings.baud_rate
            if port_name is not None:
                settings.port_name = port_name
            if baud_rate is not None:
                settings.baud_rate = baud_rate
            if not settings.is_valid():
                settings.port_name = old_port
                settings.baud_rate = old_baud
                return DeviceStatus.NOT_AVAILABLE

            self.connected = False
            self._log(
                1,
                f"Trying serial port {settings.port_name} at {settings.baud_rate} bauds.",
            )
            if self._serial_transport.connect():
                optimal_timeout = settings.timeout + 250
                status = self._arduino_available(optimal_timeout)
                self.connected = status == DeviceStatus.AVAILABLE
                if self.connected:
                    self._log(
                        1,
                        f"Connected to serial port {settings.port_name} at {settings.baud_rate} bauds.",
                    )
                    self._store_settings()
                else:
                    self._serial_transport.disconnect()
                return status
            return DeviceStatus.NOT_AVAILABLE

    # ------------------------------------------------------------------
    # ConnectionManager overrides
    # ------------------------------------------------------------------
    def _start_scan(self) -> None:
        super()._start_scan()
        if self._mode == Mode.SCAN:
            self._update_available_ports()
            self._scan_type = _ScanType.NONE

    def _do_work_connect(self) -> None:
        try:
            active = self._try_connection() == DeviceStatus.AVAILABLE
        except Exception:
            active = False
        if active:
            self._connection_found_event()

    def _do_work_scan(self) -> None:
        active = False
        if self._scan_type == _ScanType.NONE:
            try:
                active = self._try_connection() == DeviceStatus.AVAILABLE
            except Exception:
                pass
            self._scan_type = _ScanType.QUICK
        elif self._scan_type == _ScanType.QUICK:
            try:
                active = self._quick_scan()
            except Exception:
                pass
            self._scan_type = _ScanType.THOROUGH
        elif self._scan_type == _ScanType.THOROUGH:
            try:
                active = self._thorough_scan()
            except Exception:
                pass
            self._scan_type = _ScanType.QUICK
        if active:
            self._connection_found_event()

    # ------------------------------------------------------------------
    # Scans
    # ------------------------------------------------------------------
    def _candidate_baud_rates(self, port_name: str) -> List[int]:
        if not self.device_scan_baud_rate_selection:
            return [self._serial_transport.current_serial_settings.baud_rate]
        supported = serial_utils.get_supported_baud_rates(port_name)
        common = serial_utils.COMMON_BAUD_RATES
        return [b for b in common if b in supported]

    def _quick_scan(self) -> bool:
        self._log(3, "Performing quick scan.")
        if self.persistent_settings and self._stored_settings.port:
            self._log(3, "Trying last stored connection.")
            if (
                self._try_connection(self._stored_settings.port, self._stored_settings.baud_rate)
                == DeviceStatus.AVAILABLE
            ):
                return True

        for port_name in list(self.available_serial_ports):
            baud_rates = self._candidate_baud_rates(port_name)
            if baud_rates:
                self._log(1, f"Trying serial port {port_name} using {len(baud_rates)} baud rate(s).")
                for baud in baud_rates:
                    if self._mode != Mode.SCAN:
                        return False
                    status = self._try_connection(port_name, baud)
                    if status == DeviceStatus.AVAILABLE:
                        return True
                    if status == DeviceStatus.IDENTITY_MISMATCH:
                        break
            if self._new_port_scan():
                return True

        if not self.available_serial_ports:
            if self._new_port_scan():
                return True
            time.sleep(0.4)
        return False

    def _thorough_scan(self) -> bool:
        self._log(1, "Performing thorough scan.")
        if self.persistent_settings and self._stored_settings.port:
            if (
                self._try_connection(self._stored_settings.port, self._stored_settings.baud_rate)
                == DeviceStatus.AVAILABLE
            ):
                return True
        for port_name in list(self.available_serial_ports):
            baud_rates = serial_utils.get_supported_baud_rates(port_name)
            if baud_rates:
                self._log(1, f"Trying serial port {port_name} using {len(baud_rates)} baud rate(s).")
                for baud in baud_rates:
                    if self._mode != Mode.SCAN:
                        return False
                    status = self._try_connection(port_name, baud)
                    if status == DeviceStatus.AVAILABLE:
                        return True
                    if status == DeviceStatus.IDENTITY_MISMATCH:
                        break
            if self._new_port_scan():
                return True
        return False

    def _new_port_scan(self) -> bool:
        new_ports = self._new_ports_in_list()
        if not new_ports:
            return False
        wait_time = 4.0
        self._log(
            1,
            f"New port(s) {','.join(new_ports)} detected, wait for {wait_time}s before attempt to connect.",
        )
        time.sleep(wait_time)
        for port_name in new_ports:
            baud_rates = self._candidate_baud_rates(port_name)
            for baud in baud_rates:
                if self._mode != Mode.SCAN:
                    return False
                status = self._try_connection(port_name, baud)
                if status == DeviceStatus.AVAILABLE:
                    return True
                if status == DeviceStatus.IDENTITY_MISMATCH:
                    break
        return False

    def _new_ports_in_list(self) -> List[str]:
        current = serial_utils.get_port_names()
        new_ports = [p for p in current if p not in self.available_serial_ports]
        self.available_serial_ports = current
        return new_ports

    def _update_available_ports(self) -> None:
        self.available_serial_ports = serial_utils.get_port_names()

    # ------------------------------------------------------------------
    # Persistence
    # ------------------------------------------------------------------
    def _store_settings(self) -> None:
        if not self.persistent_settings:
            return
        s = self._serial_transport.current_serial_settings
        self._stored_settings.port = s.port_name
        self._stored_settings.baud_rate = s.baud_rate
        if self._connection_storer:
            self._connection_storer.save({
                "port_name": s.port_name,
                "baud_rate": s.baud_rate,
            })
        elif self._settings_store:
            self._settings_store(self._stored_settings)

    def _read_settings(self) -> None:
        if not self.persistent_settings:
            return
        if self._connection_storer:
            data = self._connection_storer.load()
            if data:
                self._stored_settings.port = data.get("port_name", "")
                self._stored_settings.baud_rate = data.get("baud_rate", 0)
        elif self._settings_load:
            loaded = self._settings_load()
            if loaded is not None:
                self._stored_settings = loaded
