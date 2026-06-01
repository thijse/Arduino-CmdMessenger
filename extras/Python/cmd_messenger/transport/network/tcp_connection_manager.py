"""``TcpConnectionManager`` — port of C# ``TcpConnectionManager``.

Simple connection manager that retries connecting to a fixed host:port and
optionally runs the watchdog.
"""
from __future__ import annotations

from typing import Optional

from ...cmd_messenger import CmdMessenger
from ...connection_manager import ConnectionManager, DeviceStatus
from .tcp_transport import TcpTransport


class TcpConnectionManager(ConnectionManager):
    """Connection manager for TCP transports."""

    def __init__(
        self,
        tcp_transport: TcpTransport,
        cmd_messenger: CmdMessenger,
        watchdog_command_id: int = 0,
        unique_device_id: Optional[str] = None,
    ) -> None:
        super().__init__(cmd_messenger, watchdog_command_id, unique_device_id)
        if tcp_transport is None:
            raise ValueError("Transport is null.")
        self._tcp_transport = tcp_transport
        # TCP doesn't scan; we always have an explicit endpoint.
        self.device_scan_enabled = False

    def _try_connection(self) -> DeviceStatus:
        self.connected = False
        self._log(1, f"Trying TCP endpoint {self._tcp_transport.host}:{self._tcp_transport.port}.")
        if not self._tcp_transport.connect():
            return DeviceStatus.NOT_AVAILABLE
        status = self._arduino_available(self._tcp_transport.timeout + 250)
        self.connected = status == DeviceStatus.AVAILABLE
        if self.connected:
            self._log(1, f"Connected to {self._tcp_transport.host}:{self._tcp_transport.port}.")
        else:
            self._tcp_transport.disconnect()
        return status

    def _do_work_connect(self) -> None:
        try:
            active = self._try_connection() == DeviceStatus.AVAILABLE
        except Exception:
            active = False
        if active:
            self._connection_found_event()

    def _do_work_scan(self) -> None:
        # No real scan for TCP — just defer to connect.
        self._do_work_connect()
