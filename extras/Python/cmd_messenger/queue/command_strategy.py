"""``CommandStrategy`` — port of C# ``CommandStrategy``."""
from __future__ import annotations

from typing import TYPE_CHECKING, Optional

from ..command import Command

if TYPE_CHECKING:
    from .list_queue import ListQueue


class CommandStrategy:
    """Wraps a :class:`Command` with per-enqueue behaviour."""

    def __init__(self, command: Command) -> None:
        self.command: Command = command
        self.command_queue: Optional["ListQueue[CommandStrategy]"] = None

    def enqueue(self) -> None:
        """Add this strategy to its command queue."""
        assert self.command_queue is not None
        self.command_queue.enqueue(self)

    def dequeue(self) -> None:
        """Remove this strategy from its command queue."""
        assert self.command_queue is not None
        try:
            self.command_queue.remove(self)
        except ValueError:
            pass
