"""``ReceiveCommandQueue`` — port of C# ``ReceiveCommandQueue``."""
from __future__ import annotations

from typing import Callable, Optional

from ..enums import SendQueue
from ..event import Event
from ..received_command import ReceivedCommand
from ..received_command_signal import ReceivedCommandSignal
from .command_queue import CommandQueue
from .command_strategy import CommandStrategy

HandleReceivedCommand = Callable[[ReceivedCommand], None]


class ReceiveCommandQueue(CommandQueue):
    """Incoming command queue with synchronous-ack support."""

    def __init__(self, received_command_handler: HandleReceivedCommand) -> None:
        super().__init__()
        #: Fires (command) for each enqueued command.
        self.new_line_received: Event = Event()

        self._received_command_handler = received_command_handler
        self._received_command_signal = ReceivedCommandSignal()

    # ------------------------------------------------------------------
    # CommandQueue overrides
    # ------------------------------------------------------------------
    def _process_queue(self) -> bool:
        with self._queue_lock:
            dequeue_command = self._dequeue_command_internal()
            has_more_work = not self.is_empty

        if dequeue_command is not None:
            self._received_command_handler(dequeue_command)

        return has_more_work

    def queue_command(self, command_strategy: CommandStrategy) -> None:  # type: ignore[override]
        # If suspended, route directly into the synchronous-wait signal.
        if self.is_suspended:
            received = command_strategy.command
            assert isinstance(received, ReceivedCommand)
            add_to_queue = self._received_command_signal.process_command(received)
            if not add_to_queue:
                return

        with self._queue_lock:
            self._queue.enqueue(command_strategy)
            for general_strategy in self._general_strategies:
                general_strategy.on_enqueue()

        if not self.is_suspended:
            self._signal_worker()
            try:
                self.new_line_received(command_strategy.command)
            except Exception:
                pass

    # ------------------------------------------------------------------
    # Public conveniences (mirror C# overloads)
    # ------------------------------------------------------------------
    def queue_received_command(self, received_command: ReceivedCommand) -> None:
        self.queue_command(CommandStrategy(received_command))

    def dequeue_command(self) -> Optional[ReceivedCommand]:
        with self._queue_lock:
            return self._dequeue_command_internal()

    def prepare_for_cmd(self, cmd_id: int, send_queue_state: SendQueue) -> None:
        self._received_command_signal.prepare_for_wait(cmd_id, send_queue_state)

    def wait_for_cmd(self, timeout_ms: int) -> Optional[ReceivedCommand]:
        return self._received_command_signal.wait_for_cmd(timeout_ms)

    # ------------------------------------------------------------------
    # Internals
    # ------------------------------------------------------------------
    def _dequeue_command_internal(self) -> Optional[ReceivedCommand]:
        # Caller holds _queue_lock.
        if self.is_empty:
            return None
        for general_strategy in self._general_strategies:
            general_strategy.on_dequeue()
        command_strategy = self._queue.dequeue()
        command = command_strategy.command
        assert isinstance(command, ReceivedCommand)
        return command
