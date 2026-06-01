"""``StaleGeneralStrategy`` — discards commands older than a timeout."""
from __future__ import annotations

from .. import time_utils
from .general_strategy import GeneralStrategy


class StaleGeneralStrategy(GeneralStrategy):
    """Drop commands from the front of the queue once they exceed ``command_timeout`` ms."""

    def __init__(self, command_timeout_ms: int) -> None:
        super().__init__()
        self._command_timeout_ms = command_timeout_ms

    def on_dequeue(self) -> None:
        assert self.command_queue is not None
        current_time = time_utils.millis()
        # Iterate from oldest (front) onward; stop at the first non-stale.
        i = 0
        # Faithful port: the C# version keeps at least one item on the queue
        # ("CommandQueue.Count > 1"). Mirror that behaviour.
        while i < len(self.command_queue):
            age = current_time - self.command_queue[i].command.time_stamp
            if age > self._command_timeout_ms and len(self.command_queue) > 1:
                del self.command_queue[i]
            else:
                break
