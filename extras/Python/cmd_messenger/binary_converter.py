"""Binary value <-> escaped-string conversion (port of C# ``BinaryConverter``).

C# uses ``BitConverter.GetBytes`` which on Windows is **little-endian**.
We use ``struct.pack/unpack`` with the ``<`` (little-endian) prefix so the
wire bytes match byte-for-byte. Strings are encoded as **ISO-8859-1 (latin-1)**
so every byte 0..255 round-trips, identical to C# ``Encoding.GetEncoding("ISO-8859-1")``.

All ``to_string`` helpers return ``None`` on failure and all parsers return
``None`` when the input has too few bytes, matching the nullable-returning C#
API. The higher-level ``ReceivedCommand`` typed readers translate ``None`` to
sentinel values (``0`` / ``""``) where needed.
"""
from __future__ import annotations

import struct
from typing import Optional

from . import escaping

# Module-level string encoder (mirrors C# ``_stringEncoder`` private static field).
_string_encoding: str = "iso-8859-1"


def set_string_encoding(encoding: str) -> None:
    """Override the string encoding used for byte<->string conversions."""
    global _string_encoding
    _string_encoding = encoding


def get_string_encoding() -> str:
    """Return the current string encoding."""
    return _string_encoding


# ---------------------------------------------------------------------------
# Binary -> escaped string
# ---------------------------------------------------------------------------

def _bytes_to_escaped_string(byte_array: bytes) -> Optional[str]:
    try:
        string_value = byte_array.decode(_string_encoding)
        return escaping.escape(string_value)
    except Exception:
        return None


def float_to_string(value: float) -> Optional[str]:
    """Convert a single-precision float to its escaped little-endian string."""
    try:
        return _bytes_to_escaped_string(struct.pack("<f", value))
    except Exception:
        return None


def double_to_string(value: float) -> Optional[str]:
    """Convert a double-precision float to its escaped little-endian string."""
    try:
        return _bytes_to_escaped_string(struct.pack("<d", value))
    except Exception:
        return None


def int32_to_string(value: int) -> Optional[str]:
    try:
        return _bytes_to_escaped_string(struct.pack("<i", value))
    except Exception:
        return None


def uint32_to_string(value: int) -> Optional[str]:
    try:
        return _bytes_to_escaped_string(struct.pack("<I", value))
    except Exception:
        return None


def int16_to_string(value: int) -> Optional[str]:
    try:
        return _bytes_to_escaped_string(struct.pack("<h", value))
    except Exception:
        return None


def uint16_to_string(value: int) -> Optional[str]:
    try:
        return _bytes_to_escaped_string(struct.pack("<H", value))
    except Exception:
        return None


def byte_to_string(value: int) -> Optional[str]:
    """Convert one byte (0..255) to its escaped string."""
    try:
        return _bytes_to_escaped_string(bytes((value & 0xFF,)))
    except Exception:
        return None


# ---------------------------------------------------------------------------
# Escaped string -> binary
# ---------------------------------------------------------------------------

def escaped_string_to_bytes(value: str) -> Optional[bytes]:
    """Inverse of :func:`_bytes_to_escaped_string`."""
    try:
        unescaped = escaping.unescape(value)
        return unescaped.encode(_string_encoding)
    except Exception:
        return None


def string_to_bytes(value: str) -> bytes:
    """Encode a string with the current encoding (no escape processing)."""
    return value.encode(_string_encoding)


def to_float(value: str) -> Optional[float]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 4:
            return None
        return struct.unpack("<f", b[:4])[0]
    except Exception:
        return None


def to_double(value: str) -> Optional[float]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 8:
            return None
        return struct.unpack("<d", b[:8])[0]
    except Exception:
        return None


def to_int32(value: str) -> Optional[int]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 4:
            return None
        return struct.unpack("<i", b[:4])[0]
    except Exception:
        return None


def to_uint32(value: str) -> Optional[int]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 4:
            return None
        return struct.unpack("<I", b[:4])[0]
    except Exception:
        return None


def to_int16(value: str) -> Optional[int]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 2:
            return None
        return struct.unpack("<h", b[:2])[0]
    except Exception:
        return None


def to_uint16(value: str) -> Optional[int]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 2:
            return None
        return struct.unpack("<H", b[:2])[0]
    except Exception:
        return None


def to_byte(value: str) -> Optional[int]:
    try:
        b = escaped_string_to_bytes(value)
        if b is None or len(b) < 1:
            return None
        return b[0]
    except Exception:
        return None
