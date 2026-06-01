"""``ListQueue`` — port of C# ``ListQueue<T>``.

Subclasses :class:`list` exactly like the C# class subclasses ``List<T>``.

.. note::
    The C# ``EnqueueFront`` calls ``Insert(Count, item)`` which is equivalent
    to ``Add(item)`` — i.e. it appends to the back, not the front. This is a
    pre-existing C# bug. We mirror the behaviour for protocol parity. To
    actually put an item at the front, use :class:`TopCommandStrategy` or
    insert at index 0 manually.
"""
from __future__ import annotations

from typing import Generic, TypeVar

T = TypeVar("T")


class ListQueue(list, Generic[T]):
    """Thin list-as-queue wrapper."""

    def enqueue(self, item: T) -> None:
        """Append item to the back of the queue."""
        self.append(item)

    def enqueue_front(self, item: T) -> None:
        """Add item to ``Count`` index (faithful C# port — actually appends).

        Mirrors the C# bug: ``Insert(Count, item)`` is identical to ``Add``.
        """
        self.insert(len(self), item)

    def dequeue(self) -> T:
        """Remove and return the item at the front."""
        return self.pop(0)

    def peek(self) -> T:
        """Return the front item without removing it."""
        return self[0]
