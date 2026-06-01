"""``SerialUtils`` — port of C# ``SerialUtils``.

Uses ``pyserial``'s :mod:`serial.tools.list_ports` to enumerate ports.
"""
from __future__ import annotations

from typing import List

try:
    from serial.tools import list_ports as _list_ports
except ImportError:  # pragma: no cover - pyserial is a hard requirement
    _list_ports = None  # type: ignore[assignment]


#: Commonly used baud rates (priority order, mirrors C#).
COMMON_BAUD_RATES: List[int] = [115200, 57600, 9600]


def get_port_names() -> List[str]:
    """Return available serial port names on this system."""
    if _list_ports is None:
        return []
    return [info.device for info in _list_ports.comports()]


def port_exists(serial_port_name: str) -> bool:
    """True iff the given port currently exists."""
    return serial_port_name in get_port_names()


def get_supported_baud_rates(serial_port_name: str) -> List[int]:
    """Best-effort baud rate enumeration.

    pyserial does not expose Windows' ``dwSettableBaud`` mask, so we return
    :data:`COMMON_BAUD_RATES`. Callers that need the full sweep can pass any
    custom list to the connection manager.
    """
    del serial_port_name  # unused
    # Return a copy so callers can mutate freely.
    return list(COMMON_BAUD_RATES)
