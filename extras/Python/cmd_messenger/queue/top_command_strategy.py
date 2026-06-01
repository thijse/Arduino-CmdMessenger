"""``TopCommandStrategy`` — adds the command at the front of the queue.

.. note::
    The C# implementation calls ``EnqueueFront`` which is buggy and actually
    appends to the back. We replicate this exact behaviour for protocol
    parity. See :class:`ListQueue` for details.
"""
from __future__ import annotations

from .command_strategy import CommandStrategy


class TopCommandStrategy(CommandStrategy):
    """Faithful port of the C# 'add to front' strategy (currently appends to back)."""

    def enqueue(self) -> None:
        assert self.command_queue is not None
        self.command_queue.enqueue_front(self)
