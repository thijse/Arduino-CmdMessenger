"""C#-style multicast event with ``+=`` / ``-=`` operator overloads.

Mirrors the behaviour of a C# ``event``: handlers can be attached and
detached with ``+=`` / ``-=`` and the event is fired by calling it.

Example:
    >>> def on_received(cmd):
    ...     print(cmd)
    >>> messenger.new_line_received += on_received
    >>> messenger.new_line_received(cmd)   # fires all handlers
    >>> messenger.new_line_received -= on_received
"""
from __future__ import annotations

from typing import Any, Callable, List


class Event:
    """Multicast event.

    Attributes:
        _handlers: Ordered list of attached callbacks.
    """

    def __init__(self) -> None:
        self._handlers: List[Callable[..., Any]] = []

    def __iadd__(self, handler: Callable[..., Any]) -> "Event":
        if not callable(handler):
            raise TypeError(f"Event handler must be callable, got {type(handler).__name__}")
        self._handlers.append(handler)
        return self

    def __isub__(self, handler: Callable[..., Any]) -> "Event":
        try:
            self._handlers.remove(handler)
        except ValueError:
            # Match C# behaviour: silently ignore detaching a handler that
            # was never attached (matches ``-=`` on an unsubscribed delegate).
            pass
        return self

    def __call__(self, *args: Any, **kwargs: Any) -> None:
        # Copy so handlers can subscribe/unsubscribe during dispatch.
        for handler in list(self._handlers):
            handler(*args, **kwargs)

    def __len__(self) -> int:
        return len(self._handlers)

    def __bool__(self) -> bool:
        return bool(self._handlers)

    def clear(self) -> None:
        """Detach all handlers."""
        self._handlers.clear()
