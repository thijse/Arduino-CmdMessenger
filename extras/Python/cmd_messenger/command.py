"""Base ``Command`` class — port of C# ``Command``."""
from __future__ import annotations

from typing import TYPE_CHECKING, List, Optional

from . import time_utils

if TYPE_CHECKING:  # avoid runtime import cycle
    from .communication_manager import CommunicationManager


class Command:
    """Base type for both sent and received commands."""

    #: Reference to the owning :class:`CommunicationManager`, injected when the
    #: command is processed. Mirrors C# ``internal CommunicationManager``.
    communication_manager: Optional["CommunicationManager"] = None

    def __init__(self) -> None:
        self.cmd_id: int = -1
        # Internal storage; first item is the command ID in C# but here we keep
        # arguments separate from cmd_id to match how C# Arguments accessor
        # behaves (returns the args list).
        self._cmd_args: List[str] = []
        self.time_stamp: int = time_utils.millis()

    @property
    def arguments(self) -> List[str]:
        """Return a copy of the argument list (matches C# ``string[] Arguments``)."""
        return list(self._cmd_args)

    @property
    def ok(self) -> bool:
        """Return whether the command is valid (``cmd_id >= 0``)."""
        return self.cmd_id >= 0

    def command_string(self) -> str:
        """Return the wire-format string ``cmd_id<fsep>arg1<fsep>arg2<csep>``.

        Requires :attr:`communication_manager` to be set (matches C# behaviour
        of throwing ``InvalidOperationException`` otherwise).
        """
        if self.communication_manager is None:
            raise RuntimeError("CommunicationManager was not set for command.")

        fsep = self.communication_manager.field_separator
        csep = self.communication_manager.command_separator
        parts = [str(self.cmd_id)]
        parts.extend(self._cmd_args)
        return fsep.join(parts) + csep
