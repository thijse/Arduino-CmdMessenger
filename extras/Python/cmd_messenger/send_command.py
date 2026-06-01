"""``SendCommand`` — port of C# ``SendCommand``.

Differences from C# (per architecture decisions 7a / Section 0):
* Varargs constructor: ``SendCommand(cmd_id, arg1, arg2, ...)``.
* ``ack_cmd_id`` / ``timeout`` are keyword-only.
* Argument typing dispatch:

  ===========  =================================================
  Python type  Wire encoding (text mode)
  -----------  -------------------------------------------------
  ``bool``     ``"1"`` / ``"0"`` (decision 4a — matches C#)
  ``int``      decimal text
  ``float``    repr() — full round-trip precision; demoted to
               ``float32`` on Bit16 boards
  ``str``      verbatim (escaping is done by CommunicationManager)
  ===========  =================================================

  Use :meth:`add_bin_argument` for explicit binary encoding.

Lazy evaluation: arguments are stored as zero-arg callables (mirroring the C#
``_lazyArguments`` list) and not materialized until :meth:`init_arguments`
runs — this allows :class:`BoardType`-dependent encoding (doubles) to be
resolved only when the command is actually placed on the wire.
"""
from __future__ import annotations

import struct
from typing import Any, Callable, List, Optional

from . import binary_converter
from .command import Command
from .enums import BoardType


class SendCommand(Command):
    """A command to be sent."""

    def __init__(
        self,
        cmd_id: int,
        *args: Any,
        ack_cmd_id: Optional[int] = None,
        timeout: int = 0,
    ) -> None:
        super().__init__()
        self._lazy_arguments: List[Callable[[], None]] = []

        self.cmd_id = cmd_id
        self.ack_cmd_id: int = ack_cmd_id if ack_cmd_id is not None else 0
        self.timeout: int = timeout
        self.req_ac: bool = ack_cmd_id is not None

        for arg in args:
            self.add_argument(arg)

    # ------------------------------------------------------------------
    # Fluent text-mode argument adders
    # ------------------------------------------------------------------
    def add_argument(self, value: Any) -> "SendCommand":
        """Add a text-mode argument. Dispatches on the Python type of ``value``.

        Returns ``self`` to allow chaining (Pythonic addition over C#).
        """
        if value is None:
            return self
        # bool must be checked BEFORE int (bool is a subclass of int in Python).
        if isinstance(value, bool):
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append("1" if v else "0"))
        elif isinstance(value, int):
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append(str(v)))
        elif isinstance(value, float):
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append(self._float_text(v)))
        elif isinstance(value, str):
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append(v))
        elif isinstance(value, (list, tuple)):
            for item in value:
                self.add_argument(item)
        else:
            raise TypeError(
                f"Unsupported argument type {type(value).__name__} for SendCommand. "
                "Convert to int/float/str/bool first, or use add_bin_argument()."
            )
        return self

    def add_arguments(self, values: Any) -> "SendCommand":
        """Add multiple text-mode arguments."""
        for v in values:
            self.add_argument(v)
        return self

    # ------------------------------------------------------------------
    # Fluent binary-mode argument adders (explicit per-type)
    # ------------------------------------------------------------------
    def add_bin_argument(self, value: Any) -> "SendCommand":
        """Add a binary-encoded argument, type-dispatched."""
        if isinstance(value, bool):
            self._lazy_arguments.append(
                lambda v=value: self._cmd_args.append(binary_converter.byte_to_string(1 if v else 0) or "")
            )
        elif isinstance(value, int):
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append(self._int_binary(v)))
        elif isinstance(value, float):
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append(self._float_binary(v)))
        elif isinstance(value, str):
            # Binary string = just escape (matches C# AddBinArgument(string))
            from . import escaping
            self._lazy_arguments.append(lambda v=value: self._cmd_args.append(escaping.escape(v)))
        else:
            raise TypeError(
                f"Unsupported binary argument type {type(value).__name__} for SendCommand."
            )
        return self

    # ------------------------------------------------------------------
    # Lazy resolution helpers
    # ------------------------------------------------------------------
    def _is_bit16(self) -> bool:
        return (
            self.communication_manager is not None
            and self.communication_manager.board_type == BoardType.BIT_16
        )

    def _float_text(self, value: float) -> str:
        # On Bit16 demote double precision through a float32 round-trip so the
        # text representation matches what a Bit16 board would emit/accept.
        if self._is_bit16():
            value = struct.unpack("<f", struct.pack("<f", value))[0]
        # ``repr`` gives the shortest round-trippable decimal for Python floats
        # — closest equivalent of C# ``ToString("R", InvariantCulture)``.
        return repr(value)

    def _float_binary(self, value: float) -> str:
        if self._is_bit16():
            return binary_converter.float_to_string(value) or ""
        return binary_converter.double_to_string(value) or ""

    def _int_binary(self, value: int) -> str:
        # Pick the smallest signed type that fits; match C# default of Int32
        # for most use cases (Int16 on 16-bit boards is implicit in the
        # protocol but Python can't infer that — caller can call
        # binary_converter.int16_to_string directly if they need narrower).
        if -(2 ** 31) <= value <= (2 ** 31) - 1:
            return binary_converter.int32_to_string(value) or ""
        if 0 <= value <= (2 ** 32) - 1:
            return binary_converter.uint32_to_string(value) or ""
        raise OverflowError(f"Integer {value} out of range for 32-bit binary encoding.")

    def init_arguments(self) -> None:
        """Materialize all lazy arguments into ``_cmd_args`` (mirrors C#)."""
        self._cmd_args.clear()
        for action in self._lazy_arguments:
            action()
