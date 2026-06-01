"""``ReceivedCommandSignal`` — direct port of C# ``ReceivedCommandSignal``.

Used by :class:`CmdMessenger` to wait synchronously for a particular acknowledge
command id on the receive thread.
"""
from __future__ import annotations

import threading
from typing import Optional

from .enums import SendQueue
from .event_waiter import EventWaiter, WaitState
from .received_command import ReceivedCommand


class ReceivedCommandSignal:
    """Block-till-ack synchronization primitive."""

    def __init__(self) -> None:
        self._cmd_id_to_match: int = -1
        self._send_queue_state: SendQueue = SendQueue.DEFAULT
        self._received_command: Optional[ReceivedCommand] = None
        self._lock = threading.Lock()
        self._waiter = EventWaiter()

    def prepare_for_wait(self, cmd_id: int, send_queue_state: SendQueue) -> None:
        """Arm the signal to fire on the next matching ``cmd_id``."""
        with self._lock:
            self._received_command = None
            self._cmd_id_to_match = cmd_id
            self._send_queue_state = send_queue_state

    def wait_for_cmd(self, timeout_ms: int) -> Optional[ReceivedCommand]:
        """Block until matching command arrives or timeout. ``None`` on timeout."""
        if self._waiter.wait_one(timeout_ms) == WaitState.TIME_OUT:
            return None
        return self._received_command

    def process_command(self, received_command: ReceivedCommand) -> bool:
        """Inspect an incoming command.

        Returns ``True`` if the command should be placed on the receive queue
        for normal callback dispatch, ``False`` if it was the awaited ack
        (consumed by the waiter).
        """
        with self._lock:
            if received_command.cmd_id == self._cmd_id_to_match:
                self._received_command = received_command
                self._waiter.set()
                return False
            return self._send_queue_state != SendQueue.CLEAR_QUEUE
