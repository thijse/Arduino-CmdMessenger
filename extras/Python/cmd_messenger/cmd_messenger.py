"""``CmdMessenger`` — main façade. Port of C# ``CmdMessenger`` class."""
from __future__ import annotations

import time
from typing import Callable, Dict, Optional

from . import escaping
from .communication_manager import CommunicationManager
from .enums import BoardType, ReceiveQueue, SendQueue, UseQueue
from .event import Event
from .queue.command_strategy import CommandStrategy
from .queue.general_strategy import GeneralStrategy
from .queue.receive_command_queue import ReceiveCommandQueue
from .queue.send_command_queue import SendCommandQueue
from .received_command import ReceivedCommand
from .send_command import SendCommand
from .transport import Transport

MessengerCallbackFunction = Callable[[ReceivedCommand], None]


class CmdMessenger:
    """Command-messaging façade over a :class:`Transport`."""

    def __init__(
        self,
        transport: Transport,
        board_type: BoardType = BoardType.BIT_16,
        field_separator: str = ",",
        command_separator: str = ";",
        escape_character: str = "/",
        send_buffer_max_length: int = 60,
    ) -> None:
        #: Fires (received_command) for each received line.
        self.new_line_received: Event = Event()
        #: Fires (sent_command) for each command actually written to the wire.
        self.new_line_sent: Event = Event()

        self._receive_command_queue = ReceiveCommandQueue(self._handle_message)
        self._communication_manager = CommunicationManager(
            transport,
            self._receive_command_queue,
            board_type,
            command_separator,
            field_separator,
            escape_character,
        )
        self._send_command_queue = SendCommandQueue(
            self._communication_manager, send_buffer_max_length
        )

        self.print_lf_cr = False

        self._receive_command_queue.new_line_received += self._on_queue_new_line_received
        self._send_command_queue.new_line_sent += self._on_queue_new_line_sent

        # Mirror C# Escaping.EscapeChars on construction so module-level
        # split/escape helpers use the same characters.
        escaping.set_escape_chars(field_separator, command_separator, escape_character)

        self._callback_list: Dict[int, MessengerCallbackFunction] = {}
        self._default_callback: Optional[MessengerCallbackFunction] = None

        self._send_command_queue.start()
        self._receive_command_queue.start()

    # ------------------------------------------------------------------
    # Public properties
    # ------------------------------------------------------------------
    @property
    def print_lf_cr(self) -> bool:
        return self._communication_manager.print_lf_cr

    @print_lf_cr.setter
    def print_lf_cr(self, value: bool) -> None:
        self._communication_manager.print_lf_cr = value

    @property
    def last_received_command_time_stamp(self) -> int:
        return self._communication_manager.last_line_time_stamp

    # ------------------------------------------------------------------
    # Connection
    # ------------------------------------------------------------------
    def connect(self) -> bool:
        return self._communication_manager.connect()

    def disconnect(self) -> bool:
        return self._communication_manager.disconnect()

    # ------------------------------------------------------------------
    # Callbacks
    # ------------------------------------------------------------------
    def attach(
        self,
        cmd_id_or_callback,
        callback: Optional[MessengerCallbackFunction] = None,
    ) -> None:
        """Attach a callback.

        Overloads:
          * ``attach(callback)``               → default callback
          * ``attach(cmd_id, callback)``       → specific command id
        """
        if callback is None:
            self._default_callback = cmd_id_or_callback
        else:
            self._callback_list[int(cmd_id_or_callback)] = callback

    # ------------------------------------------------------------------
    # Sending
    # ------------------------------------------------------------------
    def send_command(
        self,
        send_command: SendCommand,
        send_queue_state: SendQueue = SendQueue.IN_FRONT_QUEUE,
        receive_queue_state: ReceiveQueue = ReceiveQueue.DEFAULT,
        use_queue: UseQueue = UseQueue.USE_QUEUE,
    ) -> ReceivedCommand:
        """Send a command. Synchronous when ``req_ac`` or ``BYPASS_QUEUE``."""
        synchronized_send = (
            send_command.req_ac or use_queue == UseQueue.BYPASS_QUEUE
        )

        if send_command.req_ac and receive_queue_state == ReceiveQueue.DEFAULT:
            receive_queue_state = ReceiveQueue.WAIT_FOR_EMPTY_QUEUE

        if send_queue_state == SendQueue.CLEAR_QUEUE:
            self._receive_command_queue.clear()

        if receive_queue_state == ReceiveQueue.CLEAR_QUEUE:
            self._send_command_queue.clear()

        if send_queue_state == SendQueue.WAIT_FOR_EMPTY_QUEUE or (
            synchronized_send and send_queue_state == SendQueue.AT_END_QUEUE
        ):
            self._spin_until(lambda: self._send_command_queue.is_empty)

        if receive_queue_state == ReceiveQueue.WAIT_FOR_EMPTY_QUEUE:
            self._spin_until(lambda: self._receive_command_queue.is_empty)

        if synchronized_send:
            return self.send_command_sync(send_command, send_queue_state)

        if send_queue_state != SendQueue.AT_END_QUEUE:
            self._send_command_queue.send_command(send_command)
        else:
            self._send_command_queue.queue_send_command(send_command)

        rc = ReceivedCommand()
        rc.communication_manager = self._communication_manager
        return rc

    def send_command_sync(
        self, send_command: SendCommand, send_queue_state: SendQueue
    ) -> ReceivedCommand:
        result = self._communication_manager.execute_send_command(
            send_command, send_queue_state
        )
        try:
            self.new_line_sent(send_command)
        except Exception:
            pass
        return result

    def queue_command(self, command) -> None:
        """Queue at back. Accepts a :class:`SendCommand` or :class:`CommandStrategy`."""
        if isinstance(command, CommandStrategy):
            self._send_command_queue.queue_command(command)
        else:
            self._send_command_queue.queue_send_command(command)

    # ------------------------------------------------------------------
    # Strategies & queue control
    # ------------------------------------------------------------------
    def add_receive_command_strategy(self, general_strategy: GeneralStrategy) -> None:
        self._receive_command_queue.add_general_strategy(general_strategy)

    def add_send_command_strategy(self, general_strategy: GeneralStrategy) -> None:
        self._send_command_queue.add_general_strategy(general_strategy)

    def clear_receive_queue(self) -> None:
        self._receive_command_queue.clear()

    def clear_send_queue(self) -> None:
        self._send_command_queue.clear()

    # ------------------------------------------------------------------
    # Internals
    # ------------------------------------------------------------------
    def _handle_message(self, received_command: ReceivedCommand) -> None:
        callback: Optional[MessengerCallbackFunction] = None
        if received_command.ok:
            callback = self._callback_list.get(
                received_command.cmd_id, self._default_callback
            )
        else:
            received_command = ReceivedCommand()
            received_command.communication_manager = self._communication_manager

        if callback is not None:
            try:
                callback(received_command)
            except Exception:
                pass

    def _on_queue_new_line_received(self, command) -> None:
        try:
            self.new_line_received(command)
        except Exception:
            pass

    def _on_queue_new_line_sent(self, command) -> None:
        try:
            self.new_line_sent(command)
        except Exception:
            pass

    @staticmethod
    def _spin_until(predicate, poll_interval: float = 0.001) -> None:
        while not predicate():
            time.sleep(poll_interval)

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------
    def dispose(self) -> None:
        self._communication_manager.dispose()
        self._send_command_queue.dispose()
        self._receive_command_queue.dispose()

    def __enter__(self) -> "CmdMessenger":
        return self

    def __exit__(self, *exc_info) -> None:
        self.dispose()
