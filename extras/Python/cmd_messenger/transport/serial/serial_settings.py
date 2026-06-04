"""Serial port settings — port of C# ``SerialSettings``.

We re-export :class:`~serial.PARITY_*` and :class:`~serial.STOPBITS_*` as enums
so the API doesn't leak ``pyserial`` constants into user code (though the
underlying values *are* pyserial's, for direct pass-through).
"""
from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

import serial as _pyserial


class Parity(str, Enum):
    """Mirror of C# ``System.IO.Ports.Parity``."""

    NONE = _pyserial.PARITY_NONE   # 'N'
    EVEN = _pyserial.PARITY_EVEN   # 'E'
    ODD = _pyserial.PARITY_ODD     # 'O'
    MARK = _pyserial.PARITY_MARK   # 'M'
    SPACE = _pyserial.PARITY_SPACE # 'S'


class StopBits(float, Enum):
    """Mirror of C# ``System.IO.Ports.StopBits`` (values match pyserial)."""

    ONE = _pyserial.STOPBITS_ONE                   # 1
    ONE_POINT_FIVE = _pyserial.STOPBITS_ONE_POINT_FIVE  # 1.5
    TWO = _pyserial.STOPBITS_TWO                   # 2


@dataclass
class SerialSettings:
    """Serial port configuration (port of C# ``SerialSettings``).

    Defaults match C#: 9600/8/N/1, DTR off, RTS off, 500 ms timeout, empty port name.
    """

    port_name: str = ""
    baud_rate: int = 9600
    parity: Parity = Parity.NONE
    data_bits: int = 8
    stop_bits: StopBits = StopBits.ONE
    dtr_enable: bool = False
    rts_enable: bool = False
    timeout: int = 500  # milliseconds (mirrors C# value); converted to float seconds for pyserial

    def is_valid(self) -> bool:
        """Mirror of C# ``IsValid()`` — sanity check on the basics."""
        return bool(self.port_name) and self.baud_rate > 0 and self.data_bits in (5, 6, 7, 8)
