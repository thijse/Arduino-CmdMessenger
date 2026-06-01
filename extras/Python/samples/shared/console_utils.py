"""ConsoleUtils — graceful Ctrl+C and run_loop helper.

Equivalent of the C# ``ConsoleUtils.cs`` found in every sample.
"""
from __future__ import annotations

import signal
import time
from typing import Callable, Optional, Protocol


class _HasLoop(Protocol):
    run_loop: bool
    def loop(self) -> None: ...


class ConsoleUtils:
    """Singleton-like namespace for console-app lifecycle helpers."""

    on_close: Optional[Callable[[], None]] = None

    @classmethod
    def run_loop(cls, logic: _HasLoop, interval: float = 0.0) -> None:
        """Run ``logic.loop()`` in a loop until ``logic.run_loop`` is False or Ctrl+C."""
        original = signal.getsignal(signal.SIGINT)

        def _handler(signum, frame):
            logic.run_loop = False

        signal.signal(signal.SIGINT, _handler)
        try:
            while logic.run_loop:
                logic.loop()
                if interval > 0:
                    time.sleep(interval)
        finally:
            signal.signal(signal.SIGINT, original)
            if cls.on_close is not None:
                cls.on_close()
