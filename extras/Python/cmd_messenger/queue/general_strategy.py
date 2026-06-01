"""``GeneralStrategy`` — port of C# ``GeneralStrategy``.

Hook points called for every enqueue/dequeue on a :class:`CommandQueue`.
Subclasses override :meth:`on_enqueue` and/or :meth:`on_dequeue`.
"""
from __future__ import annotations

from typing import TYPE_CHECKING, Optional

if TYPE_CHECKING:
    from .command_strategy import CommandStrategy
    from .list_queue import ListQueue


class GeneralStrategy:
    def __init__(self) -> None:
        self.command_queue: Optional["ListQueue[CommandStrategy]"] = None

    def on_enqueue(self) -> None:
        """Called after a command has been enqueued."""

    def on_dequeue(self) -> None:
        """Called before a command is dequeued."""
