"""``EventWaiter`` — direct port of C# ``EventWaiter``.

C# uses ``Monitor.Wait``/``Monitor.Pulse`` for an in-process AutoResetEvent-like
primitive. Python's equivalent is :class:`threading.Condition`, which gives us
the same semantics: a single signal that releases one waiter, with optional
timeout.

We mirror the C# API surface 1:1 — :meth:`wait_one`, :meth:`set`, :meth:`reset`,
plus the :class:`WaitState` enum.
"""
from __future__ import annotations

import threading
import time
from enum import Enum


class WaitState(Enum):
    """Outcome of :meth:`EventWaiter.wait_one`."""

    TIME_OUT = "TimeOut"
    NORMAL = "Normal"


class EventWaiter:
    """Single-event signal with timeout, modelled on C# ``EventWaiter``."""

    def __init__(self, set: bool = False) -> None:
        """Create a waiter.

        Args:
            set: If ``True`` the first :meth:`wait_one` returns immediately.
        """
        self._condition = threading.Condition()
        self._block = not set

    def wait_one(self, timeout_ms: int) -> WaitState:
        """Block until signalled or the timeout (ms) elapses.

        Pass a negative timeout (or ``-1``) to wait indefinitely (mirrors C#
        ``Timeout.Infinite``).
        """
        with self._condition:
            # Already signalled?
            if not self._block:
                self._block = True
                return WaitState.NORMAL

            if timeout_ms is None or timeout_ms < 0:
                # Infinite wait — loop until block clears.
                while self._block:
                    self._condition.wait()
                self._block = True
                return WaitState.NORMAL

            timeout_s = timeout_ms / 1000.0
            deadline = time.monotonic() + timeout_s
            timed_out = False
            while self._block:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    timed_out = True
                    break
                # Condition.wait returns True if notified, False if timed out;
                # we re-check ``_block`` in the loop guard either way.
                self._condition.wait(remaining)

            # Re-block for next entry, matching C# semantics.
            self._block = True
            return WaitState.TIME_OUT if timed_out else WaitState.NORMAL

    def set(self) -> None:
        """Signal — unblocks one waiter."""
        with self._condition:
            self._block = False
            self._condition.notify()

    def reset(self) -> None:
        """Reset — subsequent :meth:`wait_one` calls will block."""
        with self._condition:
            self._block = True
