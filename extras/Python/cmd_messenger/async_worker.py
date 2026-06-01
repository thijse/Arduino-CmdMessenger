"""``AsyncWorker`` — direct port of C# ``AsyncWorker``.

A background thread that repeatedly calls a user-supplied job. The job returns
``True`` if there is more work to do (loop immediately), ``False`` to wait
until :meth:`Signal` is called.

Lifecycle states mirror the C# enum exactly: ``STOPPED``, ``RUNNING``,
``SUSPENDED``. The API surface is :meth:`start` / :meth:`stop` / :meth:`suspend`
/ :meth:`resume` / :meth:`signal`.
"""
from __future__ import annotations

import threading
import time
from enum import Enum
from typing import Callable, Optional

from .event_waiter import EventWaiter

#: Type alias for the worker callback. Returns ``True`` to be called again
#: immediately, ``False`` to wait until signalled.
AsyncWorkerJob = Callable[[], bool]


class WorkerState(Enum):
    STOPPED = "Stopped"
    RUNNING = "Running"
    SUSPENDED = "Suspended"


def _spin_wait_until(predicate: Callable[[], bool], step_s: float = 0.001) -> None:
    """Busy-wait equivalent to C# ``SpinWait.SpinUntil`` with a tiny sleep."""
    while not predicate():
        time.sleep(step_s)


class AsyncWorker:
    """Background-thread worker loop."""

    def __init__(self, worker_job: AsyncWorkerJob, name: Optional[str] = None) -> None:
        if worker_job is None:
            raise ValueError("worker_job is required")
        self._worker_job = worker_job
        self.name = name

        self._state = WorkerState.STOPPED
        self._requested_state = WorkerState.STOPPED
        self._is_faulted = False

        self._lock = threading.Lock()
        self._event_waiter = EventWaiter()
        self._worker_thread: Optional[threading.Thread] = None

    # ------------------------------------------------------------------
    # State accessors (mirror C# properties)
    # ------------------------------------------------------------------
    @property
    def state(self) -> WorkerState:
        return self._state

    @property
    def is_running(self) -> bool:
        return self._state == WorkerState.RUNNING

    @property
    def is_suspended(self) -> bool:
        return self._state == WorkerState.SUSPENDED

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------
    def start(self) -> None:
        with self._lock:
            if self._state != WorkerState.STOPPED:
                raise RuntimeError("The worker is already started.")
            self._requested_state = self._state = WorkerState.RUNNING
            self._is_faulted = False
            self._event_waiter.reset()

            self._worker_thread = threading.Thread(
                target=self._run, name=self.name, daemon=True
            )
            self._worker_thread.start()
            _spin_wait_until(lambda: self._worker_thread is not None and self._worker_thread.is_alive())

    def stop(self) -> None:
        with self._lock:
            if self._state in (WorkerState.RUNNING, WorkerState.SUSPENDED):
                self._requested_state = WorkerState.STOPPED
                # Avoid deadlock if called from inside the worker thread.
                if threading.current_thread() is not self._worker_thread:
                    self._event_waiter.set()
                    if self._worker_thread is not None:
                        self._worker_thread.join()
            elif not self._is_faulted:
                raise RuntimeError("The worker is already stopped.")

    def suspend(self) -> None:
        with self._lock:
            if self._state != WorkerState.RUNNING:
                raise RuntimeError("The worker is not running.")
            self._requested_state = WorkerState.SUSPENDED
            self._event_waiter.set()
            _spin_wait_until(lambda: self._requested_state == self._state)

    def resume(self) -> None:
        with self._lock:
            if self._state != WorkerState.SUSPENDED:
                raise RuntimeError("The worker is not in suspended state.")
            self._requested_state = WorkerState.RUNNING
            self._event_waiter.set()
            _spin_wait_until(lambda: self._requested_state == self._state)

    def signal(self) -> None:
        """Wake the worker if it's idle (matches C# ``Signal``)."""
        if self.is_running:
            self._event_waiter.set()

    # ------------------------------------------------------------------
    # Internal
    # ------------------------------------------------------------------
    def _run(self) -> None:
        while True:
            if self._state == WorkerState.STOPPED:
                break

            have_more_work = False
            if self._state == WorkerState.RUNNING:
                try:
                    have_more_work = self._worker_job()
                except Exception:
                    self._requested_state = self._state = WorkerState.STOPPED
                    self._is_faulted = True
                    raise

                # The job may have requested a state change.
                if (
                    self._requested_state != self._state
                    and self._requested_state == WorkerState.STOPPED
                ):
                    self._state = self._requested_state
                    break

            if not have_more_work or self._state == WorkerState.SUSPENDED:
                self._event_waiter.wait_one(-1)  # infinite
            self._state = self._requested_state
