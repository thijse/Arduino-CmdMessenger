"""``CollapseCommandStrategy`` — replaces any pending command with the same ``cmd_id``."""
from __future__ import annotations

from .command_strategy import CommandStrategy


class CollapseCommandStrategy(CommandStrategy):
    """Avoid duplicate commands of the same id in the queue (avoids lag)."""

    def enqueue(self) -> None:
        assert self.command_queue is not None
        for i, strategy in enumerate(self.command_queue):
            if strategy.command.cmd_id == self.command.cmd_id:
                self.command_queue[i] = self
                return
        self.command_queue.enqueue(self)
