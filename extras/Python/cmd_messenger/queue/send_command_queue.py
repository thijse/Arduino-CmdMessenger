"""``SendCommandQueue`` — port of C# ``SendCommandQueue``.

The worker drains the queue, packing as many non-ack commands as fit into a
buffer, then handing the buffer to the :class:`CommunicationManager` for
transmission. Commands requiring an acknowledge are sent one at a time.
"""
from __future__ import annotations

import threading
import time
from typing import TYPE_CHECKING, Optional

from ..enums import SendQueue
from ..event import Event
from ..send_command import SendCommand
from .command_strategy import CommandStrategy
from .command_queue import CommandQueue
from .top_command_strategy import TopCommandStrategy

if TYPE_CHECKING:
    from ..communication_manager import CommunicationManager


class SendCommandQueue(CommandQueue):
    """Outgoing command queue."""

    def __init__(self, communication_manager: "CommunicationManager", send_buffer_max_length: int = 62) -> None:
        super().__init__()
        #: Fires (command) when a command has actually been written to the wire.
        self.new_line_sent: Event = Event()

        self._communication_manager = communication_manager
        self._send_buffer_max_length = send_buffer_max_length
        self.max_queue_length: int = 5000

        self._send_buffer: str = ""
        self._command_count: int = 0

    # ------------------------------------------------------------------
    # CommandQueue overrides
    # ------------------------------------------------------------------
    def _process_queue(self) -> bool:
        self._send_commands_from_queue()
        with self._queue_lock:
            return not self.is_empty

    def queue_command(self, command_strategy: CommandStrategy) -> None:  # type: ignore[override]
        # Backpressure: spin-wait until queue is below max length.
        while len(self._queue) > self.max_queue_length:
            time.sleep(0)  # like Thread.Yield

        with self._queue_lock:
            command_strategy.command_queue = self._queue
            command_strategy.command.communication_manager = self._communication_manager
            assert isinstance(command_strategy.command, SendCommand)
            command_strategy.command.init_arguments()

            command_strategy.enqueue()

            for general_strategy in self._general_strategies:
                general_strategy.on_enqueue()

        self._signal_worker()

    # ------------------------------------------------------------------
    # Convenience wrappers (mirror C# overloads)
    # ------------------------------------------------------------------
    def send_command(self, send_command: SendCommand) -> None:
        """Queue at front via :class:`TopCommandStrategy`."""
        self.queue_command(TopCommandStrategy(send_command))

    def queue_send_command(self, send_command: SendCommand) -> None:
        """Queue at back via default :class:`CommandStrategy`."""
        self.queue_command(CommandStrategy(send_command))

    # ------------------------------------------------------------------
    # Internals — direct port of C# SendCommandsFromQueue
    # ------------------------------------------------------------------
    def _send_commands_from_queue(self) -> None:
        self._command_count = 0
        self._send_buffer = ""
        event_command_strategy: Optional[CommandStrategy] = None

        while len(self._send_buffer) < self._send_buffer_max_length and len(self._queue) > 0:
            with self._queue_lock:
                command_strategy = self._queue.peek() if not self.is_empty else None
                if command_strategy is None:
                    break
                if command_strategy.command is None:
                    break

                send_command = command_strategy.command
                assert isinstance(send_command, SendCommand)

                if send_command.req_ac:
                    if self._command_count > 0:
                        break
                    self._send_single_command_from_queue(command_strategy)
                else:
                    event_command_strategy = command_strategy
                    self._add_to_command_string(command_strategy)

            # Fire event outside the lock for performance (matches C# comment).
            if event_command_strategy is not None:
                try:
                    self.new_line_sent(event_command_strategy.command)
                except Exception:
                    pass
                event_command_strategy = None

        # Flush packed buffer.
        if len(self._send_buffer) > 0:
            self._communication_manager.execute_send_string(self._send_buffer, SendQueue.IN_FRONT_QUEUE)

    def _send_single_command_from_queue(self, command_strategy: CommandStrategy) -> None:
        # Caller already holds _queue_lock.
        command_strategy.dequeue()
        for general_strategy in self._general_strategies:
            general_strategy.on_dequeue()
        if command_strategy.command is not None:
            assert isinstance(command_strategy.command, SendCommand)
            self._communication_manager.execute_send_command(command_strategy.command, SendQueue.IN_FRONT_QUEUE)

    def _add_to_command_string(self, command_strategy: CommandStrategy) -> None:
        # Caller already holds _queue_lock.
        command_strategy.dequeue()
        for general_strategy in self._general_strategies:
            general_strategy.on_dequeue()
        if command_strategy.command is not None:
            self._command_count += 1
            self._send_buffer += command_strategy.command.command_string()
            if getattr(self._communication_manager, "print_lf_cr", False):
                self._send_buffer += "\r\n"
