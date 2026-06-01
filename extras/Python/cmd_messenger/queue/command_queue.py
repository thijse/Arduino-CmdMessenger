"""``CommandQueue`` — abstract base. Direct port of C# ``CommandQueue``.

Each queue owns an :class:`AsyncWorker`. Concrete subclasses implement
:meth:`_process_queue` which is the worker job.
"""
from __future__ import annotations

import threading
from abc import ABC, abstractmethod
from typing import List

from ..async_worker import AsyncWorker
from .command_strategy import CommandStrategy
from .general_strategy import GeneralStrategy
from .list_queue import ListQueue


class CommandQueue(ABC):
    """Abstract base for the send/receive command queues."""

    def __init__(self) -> None:
        self._queue: ListQueue[CommandStrategy] = ListQueue()
        self._queue_lock = threading.Lock()
        self._general_strategies: List[GeneralStrategy] = []
        self._worker = AsyncWorker(self._process_queue, name=self.__class__.__name__)

    # ------------------------------------------------------------------
    # Worker passthrough (mirrors C# properties)
    # ------------------------------------------------------------------
    @property
    def is_running(self) -> bool:
        return self._worker.is_running

    @property
    def is_suspended(self) -> bool:
        return self._worker.is_suspended

    @property
    def count(self) -> int:
        """Number of pending commands. NOT thread-safe (matches C# note)."""
        return len(self._queue)

    @property
    def is_empty(self) -> bool:
        return len(self._queue) == 0

    def clear(self) -> None:
        with self._queue_lock:
            self._queue.clear()

    def add_general_strategy(self, general_strategy: GeneralStrategy) -> None:
        general_strategy.command_queue = self._queue
        self._general_strategies.append(general_strategy)

    @abstractmethod
    def queue_command(self, command_strategy: CommandStrategy) -> None:
        """Queue the wrapped strategy. Subclass-specific behaviour."""

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------
    def start(self) -> None:
        self._worker.start()

    def stop(self) -> None:
        self._worker.stop()
        self.clear()

    def suspend(self) -> None:
        self._worker.suspend()

    def resume(self) -> None:
        self._worker.resume()

    def _signal_worker(self) -> None:
        self._worker.signal()

    def dispose(self) -> None:
        if self._worker.state.name != "STOPPED":
            self.stop()

    def __enter__(self) -> "CommandQueue":
        self.start()
        return self

    def __exit__(self, *exc_info) -> None:
        self.dispose()

    # ------------------------------------------------------------------
    # Worker job — subclass-supplied
    # ------------------------------------------------------------------
    @abstractmethod
    def _process_queue(self) -> bool:
        """Return ``True`` if more work pending, ``False`` to wait for signal."""
