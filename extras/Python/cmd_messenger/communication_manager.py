"""``CommunicationManager`` — port of C# ``CommunicationManager``.

Sits between the transport layer (raw bytes) and the receive command queue
(parsed :class:`ReceivedCommand` objects), and provides the synchronous
``execute_send_*`` methods used by both the send queue and the synchronous
:meth:`CmdMessenger.send_command_sync` path.
"""
from __future__ import annotations

import threading
from typing import TYPE_CHECKING, Optional

from . import escaping, time_utils
from .enums import BoardType, SendQueue
from .escaping import IsEscaped
from .received_command import ReceivedCommand
from .send_command import SendCommand
from .transport import Transport

if TYPE_CHECKING:
    from .queue.receive_command_queue import ReceiveCommandQueue


class CommunicationManager:
    """Manager for data over the transport layer."""

    def __init__(
        self,
        transport: Transport,
        receive_command_queue: "ReceiveCommandQueue",
        board_type: BoardType,
        command_separator: str,
        field_separator: str,
        escape_character: str,
    ) -> None:
        self._transport = transport
        self._receive_command_queue = receive_command_queue

        self.board_type: BoardType = board_type
        self.command_separator: str = command_separator
        self.field_separator: str = field_separator
        self.escape_character: str = escape_character

        #: Whether to append ``\r\n`` after every written command.
        self.print_lf_cr: bool = False
        #: Time-stamp (ms) of the most recently received line.
        self.last_line_time_stamp: int = 0

        self._send_command_data_lock = threading.RLock()
        self._parse_lines_lock = threading.Lock()
        self._buffer: str = ""
        self._is_escaped = IsEscaped()

        # Wire transport → parser.
        self._transport.data_received += self._new_data_received

    # ------------------------------------------------------------------
    # Connection
    # ------------------------------------------------------------------
    def connect(self) -> bool:
        if self._transport.is_connected():
            return False
        return self._transport.connect()

    def disconnect(self) -> bool:
        if not self._transport.is_connected():
            return False
        return self._transport.disconnect()

    # ------------------------------------------------------------------
    # Raw write
    # ------------------------------------------------------------------
    def write(self, value: str) -> None:
        """Write a string (ISO-8859-1 encoded) to the transport."""
        self._transport.write(value.encode("iso-8859-1"))

    def write_line(self, value: str) -> None:
        """Write a string followed by ``\\r\\n``."""
        self.write(value + "\r\n")

    # ------------------------------------------------------------------
    # Synchronous send paths used by SendCommandQueue
    # ------------------------------------------------------------------
    def execute_send_command(
        self, send_command: SendCommand, send_queue_state: SendQueue
    ) -> ReceivedCommand:
        """Send a command, optionally waiting for an acknowledge.

        Returns a :class:`ReceivedCommand`. The result is only meaningful when
        ``send_command.req_ac`` is true; otherwise an empty stub is returned.
        """
        with self._send_command_data_lock:
            send_command.communication_manager = self
            send_command.init_arguments()

            if send_command.req_ac:
                self._receive_command_queue.suspend()
                self._receive_command_queue.prepare_for_cmd(
                    send_command.ack_cmd_id, send_queue_state
                )
                self._write_command(send_command)
                rc = self._receive_command_queue.wait_for_cmd(send_command.timeout)
                ack_command = rc if rc is not None else ReceivedCommand()
            else:
                self._write_command(send_command)
                ack_command = ReceivedCommand()

            ack_command.communication_manager = self

        if send_command.req_ac:
            self._receive_command_queue.resume()

        return ack_command

    def execute_send_string(
        self, command_string: str, send_queue_state: SendQueue
    ) -> ReceivedCommand:
        """Send a pre-formatted command string to the transport."""
        with self._send_command_data_lock:
            if self.print_lf_cr:
                self.write_line(command_string)
            else:
                self.write(command_string)
        rc = ReceivedCommand()
        rc.communication_manager = self
        return rc

    # ------------------------------------------------------------------
    # Parsing pipeline
    # ------------------------------------------------------------------
    def _new_data_received(self, *_args, **_kwargs) -> None:
        self._parse_lines()

    def _parse_lines(self) -> None:
        with self._parse_lines_lock:
            data = self._transport.read()
            if data:
                self._buffer += data.decode("iso-8859-1")
            while True:
                current_line = self._parse_line()
                if not current_line:
                    break
                self.last_line_time_stamp = time_utils.millis()
                self._process_line(current_line)

    def _process_line(self, line: str) -> None:
        received = self._parse_message(line)
        received.raw_string = line
        received.time_stamp = self.last_line_time_stamp
        self._receive_command_queue.queue_received_command(received)

    def _parse_message(self, line: str) -> ReceivedCommand:
        cleaned = line.strip("\r\n")
        cleaned = escaping.remove(cleaned, self.command_separator, self.escape_character)
        args = escaping.split(
            cleaned,
            self.field_separator,
            self.escape_character,
            remove_empty_entries=True,
        )
        rc = ReceivedCommand(args)
        rc.communication_manager = self
        return rc

    def _parse_line(self) -> str:
        if not self._buffer:
            return ""
        i = self._find_next_eol()
        if 0 <= i < len(self._buffer):
            line = self._buffer[: i + 1]
            self._buffer = self._buffer[i + 1 :]
            return line
        return ""

    def _find_next_eol(self) -> int:
        pos = 0
        while pos < len(self._buffer):
            escaped = self._is_escaped.escaped_char(self._buffer[pos])
            if self._buffer[pos] == self.command_separator and not escaped:
                return pos
            pos += 1
        return pos

    def _write_command(self, send_command: SendCommand) -> None:
        if self.print_lf_cr:
            self.write_line(send_command.command_string())
        else:
            self.write(send_command.command_string())

    # ------------------------------------------------------------------
    # Disposal
    # ------------------------------------------------------------------
    def dispose(self) -> None:
        try:
            self._transport.data_received -= self._new_data_received
        except Exception:
            pass

    def __enter__(self) -> "CommunicationManager":
        return self

    def __exit__(self, *exc_info) -> None:
        self.dispose()
