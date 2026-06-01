"""``ReceivedCommand`` — port of C# ``ReceivedCommand``.

Provides two reader APIs (decision 7b):

1. **Iterator-style** (C# parity): ``read_int32_arg()``, ``read_float_arg()``,
   ``read_string_arg()``, ``read_bool_arg()``, etc. — and the binary variants
   ``read_bin_*_arg()``. Each call advances the internal parameter cursor.

2. **Format-string** (Pythonic): :meth:`read` parses a format string and
   returns a tuple. Codes mirror :mod:`struct` where sensible:

   =====  =========================  ==============================
   code   text type                  binary variant (``*`` prefix)
   =====  =========================  ==============================
   ``i``  int32                      ``*i``
   ``I``  uint32                     ``*I``
   ``h``  int16                      ``*h``
   ``H``  uint16                     ``*H``
   ``b``  byte (signed)              ``*b``
   ``B``  byte (unsigned)            ``*B``
   ``f``  float (single)             ``*f``
   ``d``  double (BoardType-aware)   ``*d``
   ``s``  string                     ``*s`` (escape-decoded bytes)
   ``?``  bool                       ``*?``
   ``c``  single char                ``*c``
   =====  =========================  ==============================

   Trailing ``*`` repeats the previous code for all remaining args:
   ``read("f*")`` reads every remaining argument as a float.
"""
from __future__ import annotations

from typing import Any, List, Optional, Tuple, Union

from . import binary_converter, escaping
from .command import Command
from .enums import BoardType


class ReceivedCommand(Command):
    """A command received from the Arduino."""

    def __init__(self, raw_arguments: Optional[List[str]] = None) -> None:
        super().__init__()
        self.raw_string: str = ""
        self._parameter: int = -1
        self._dumped: bool = True

        if not raw_arguments:
            return

        try:
            self.cmd_id = int(raw_arguments[0])
        except (ValueError, IndexError):
            self.cmd_id = -1
            return

        if self.cmd_id < 0:
            return

        if len(raw_arguments) > 1:
            self._cmd_args.extend(raw_arguments[1:])

    # ------------------------------------------------------------------
    # Iterator
    # ------------------------------------------------------------------
    def next(self) -> bool:
        """Advance to the next argument. Returns ``False`` when exhausted."""
        if self._dumped:
            if self._parameter < len(self._cmd_args) - 1:
                self._parameter += 1
                self._dumped = False
                return True
            return False
        return True

    def available(self) -> bool:
        """Return whether another argument is available (alias of :meth:`next`)."""
        return self.next()

    def __iter__(self):
        """Iterate over the raw string arguments."""
        return iter(list(self._cmd_args))

    # ------------------------------------------------------------------
    # Text-mode typed readers (mirror C# ReadXxxArg)
    # ------------------------------------------------------------------
    def _current(self) -> str:
        return self._cmd_args[self._parameter]

    def read_int16_arg(self) -> int:
        if self.next():
            try:
                v = int(self._current())
                # Range check to mimic C# Int16.TryParse failure -> 0.
                if -32768 <= v <= 32767:
                    self._dumped = True
                    return v
            except ValueError:
                pass
        return 0

    def read_uint16_arg(self) -> int:
        if self.next():
            try:
                v = int(self._current())
                if 0 <= v <= 65535:
                    self._dumped = True
                    return v
            except ValueError:
                pass
        return 0

    def read_int32_arg(self) -> int:
        if self.next():
            try:
                v = int(self._current())
                if -(2 ** 31) <= v <= (2 ** 31) - 1:
                    self._dumped = True
                    return v
            except ValueError:
                pass
        return 0

    def read_uint32_arg(self) -> int:
        if self.next():
            try:
                v = int(self._current())
                if 0 <= v <= (2 ** 32) - 1:
                    self._dumped = True
                    return v
            except ValueError:
                pass
        return 0

    def read_float_arg(self) -> float:
        if self.next():
            try:
                v = float(self._current())
                self._dumped = True
                return v
            except ValueError:
                pass
        return 0.0

    def read_double_arg(self) -> float:
        # Matches C#: requires comm manager because Bit16 demotes to float.
        if self.communication_manager is None:
            raise RuntimeError("CommunicationManager was not set for command.")
        if self.next():
            try:
                v = float(self._current())
                self._dumped = True
                return v
            except ValueError:
                pass
        return 0.0

    def read_string_arg(self) -> str:
        if self.next():
            v = self._current()
            if v is not None:
                self._dumped = True
                return v
        return ""

    def read_bool_arg(self) -> bool:
        return self.read_int32_arg() != 0

    def read_char_arg(self) -> str:
        s = self.read_string_arg()
        return s[0] if s else "\0"

    # ------------------------------------------------------------------
    # Binary-mode readers (mirror C# ReadBinXxxArg)
    # ------------------------------------------------------------------
    def read_bin_int16_arg(self) -> int:
        if self.next():
            v = binary_converter.to_int16(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0

    def read_bin_uint16_arg(self) -> int:
        if self.next():
            v = binary_converter.to_uint16(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0

    def read_bin_int32_arg(self) -> int:
        if self.next():
            v = binary_converter.to_int32(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0

    def read_bin_uint32_arg(self) -> int:
        if self.next():
            v = binary_converter.to_uint32(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0

    def read_bin_float_arg(self) -> float:
        if self.next():
            v = binary_converter.to_float(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0.0

    def read_bin_double_arg(self) -> float:
        if self.communication_manager is None:
            raise RuntimeError("CommunicationManager was not set for command.")
        if self.next():
            if self.communication_manager.board_type == BoardType.BIT_16:
                v: Optional[float] = binary_converter.to_float(self._current())
            else:
                v = binary_converter.to_double(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0.0

    def read_bin_byte_arg(self) -> int:
        if self.next():
            v = binary_converter.to_byte(self._current())
            if v is not None:
                self._dumped = True
                return v
        return 0

    def read_bin_string_arg(self) -> str:
        if self.next():
            b = binary_converter.escaped_string_to_bytes(self._current())
            if b is not None:
                self._dumped = True
                return b.decode(binary_converter.get_string_encoding())
        return ""

    def read_bin_bool_arg(self) -> bool:
        return self.read_bin_byte_arg() != 0

    # ------------------------------------------------------------------
    # Format-string reader (Pythonic extra — decision 7b)
    # ------------------------------------------------------------------
    _TEXT_READERS = {
        "i": "read_int32_arg",
        "I": "read_uint32_arg",
        "h": "read_int16_arg",
        "H": "read_uint16_arg",
        "b": "read_int16_arg",   # signed byte → narrow int16 reader is fine
        "B": "read_uint16_arg",  # unsigned byte
        "f": "read_float_arg",
        "d": "read_double_arg",
        "s": "read_string_arg",
        "?": "read_bool_arg",
        "c": "read_char_arg",
    }
    _BIN_READERS = {
        "i": "read_bin_int32_arg",
        "I": "read_bin_uint32_arg",
        "h": "read_bin_int16_arg",
        "H": "read_bin_uint16_arg",
        "b": "read_bin_byte_arg",
        "B": "read_bin_byte_arg",
        "f": "read_bin_float_arg",
        "d": "read_bin_double_arg",
        "s": "read_bin_string_arg",
        "?": "read_bin_bool_arg",
    }

    def read(self, fmt: str) -> Union[Any, Tuple[Any, ...]]:
        """Read arguments according to a format string. See module docstring.

        Returns a single value when ``fmt`` resolves to one read, otherwise a
        tuple — matching PyCmdMessenger ergonomics.
        """
        tokens = self._parse_format(fmt)
        results: List[Any] = []
        for is_binary, code in tokens:
            table = self._BIN_READERS if is_binary else self._TEXT_READERS
            reader_name = table.get(code)
            if reader_name is None:
                raise ValueError(f"Unknown format code {'*' if is_binary else ''}{code!r}")
            results.append(getattr(self, reader_name)())
        if len(results) == 1:
            return results[0]
        return tuple(results)

    def _parse_format(self, fmt: str) -> List[Tuple[bool, str]]:
        """Expand ``fmt`` to a list of ``(is_binary, code)`` tokens."""
        tokens: List[Tuple[bool, str]] = []
        i = 0
        n = len(fmt)
        last: Optional[Tuple[bool, str]] = None
        while i < n:
            ch = fmt[i]
            if ch == "*":
                # Trailing '*' alone repeats the previous code for all
                # remaining arguments not yet consumed by earlier tokens.
                if i == n - 1:
                    if last is None:
                        raise ValueError(
                            "Trailing '*' has no preceding format code to repeat."
                        )
                    # Args remaining at read() entry, accounting for cursor + dumped flag.
                    remaining = len(self._cmd_args) - (
                        self._parameter + (1 if self._dumped else 0)
                    )
                    remaining -= len(tokens)
                    for _ in range(max(remaining, 0)):
                        tokens.append(last)
                    i += 1
                    continue
                # Otherwise '*X' = binary code.
                if i + 1 >= n:
                    raise ValueError("'*' must be followed by a format code.")
                code = fmt[i + 1]
                token = (True, code)
                tokens.append(token)
                last = token
                i += 2
            else:
                token = (False, ch)
                tokens.append(token)
                last = token
                i += 1
        return tokens
