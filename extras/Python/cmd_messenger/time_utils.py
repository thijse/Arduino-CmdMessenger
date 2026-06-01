"""Time helpers — port of C# ``TimeUtils``.

The C# version exposes static properties ``Millis`` and ``Seconds`` returning
the milliseconds / seconds since 1 Jan 1970 (Unix epoch in UTC).
We expose them as plain module-level functions to stay Pythonic.
"""
from __future__ import annotations

import time
from typing import List


def millis() -> int:
    """Return milliseconds since the Unix epoch (UTC)."""
    return int(time.time() * 1000)


def seconds() -> int:
    """Return seconds since the Unix epoch (UTC)."""
    return int(time.time())


def has_expired(prev_time: List[int], interval: int) -> bool:
    """Return ``True`` if ``interval`` ms have passed since ``prev_time[0]``.

    Because Python ints are immutable, the C# ``ref long prevTime`` pattern is
    emulated by passing a single-element list which the function mutates in
    place. On expiry the slot is updated to ``millis()`` so the next call
    measures a fresh interval.

    Args:
        prev_time: 1-element mutable list holding the previous timestamp (ms).
        interval: Interval in milliseconds.

    Returns:
        ``True`` if more than ``interval`` ms have elapsed since ``prev_time[0]``.
    """
    now = millis()
    if now - prev_time[0] > interval:
        prev_time[0] = now
        return True
    return False
