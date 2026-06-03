"""Test helpers that emulate the embedded side of CmdMessenger.

These helpers are intentionally small protocol doubles. They let the Python host
stack exercise ACK, parsing, escaping, text typed data, binary typed data, and
multi-argument command flows without requiring a serial port.
"""
from __future__ import annotations

import threading
from collections import deque
from enum import IntEnum
from typing import Callable

from cmd_messenger import escaping
from cmd_messenger.transport.transport import Transport


class LoopbackCommand(IntEnum):
    ACKNOWLEDGE = 0
    ERROR = 1
    ECHO = 2
    ECHO_RESULT = 3
    ADD_FLOATS = 4
    ADD_FLOATS_RESULT = 5
    ECHO_INT = 6
    ECHO_INT_RESULT = 7
    ECHO_BOOL = 8
    ECHO_BOOL_RESULT = 9
    MULTI_ARGS = 10
    MULTI_ARGS_RESULT = 11
    PING = 12
    PONG = 13
    ECHO_INT16 = 14
    ECHO_INT16_RESULT = 15
    ECHO_DOUBLE = 16
    ECHO_DOUBLE_RESULT = 17


class LegacyCommand(IntEnum):
    ARE_YOU_READY = 20
    ACK = 21
    VALUE_PING = 22
    VALUE_PONG = 23
    MULTI_VALUE_PING = 24
    MULTI_VALUE_PONG = 25


class DataType(IntEnum):
    BOOL = 0
    INT16 = 1
    INT32 = 2
    FLOAT = 3
    FLOAT_SCI = 4
    DOUBLE = 5
    DOUBLE_SCI = 6
    CHAR = 7
    STRING = 8
    BBOOL = 9
    BINT16 = 10
    BINT32 = 11
    BFLOAT = 12
    BDOUBLE = 13
    BCHAR = 14
    ESC_STRING = 15


class SimulatedFirmwareTransport(Transport):
    """Transport whose write path is handled by an in-process firmware callback."""

    def __init__(self, handler: Callable[[int, list[str]], list[tuple[int, list[str]]]]) -> None:
        super().__init__()
        self._handler = handler
        self._buffer: deque[bytes] = deque()
        self._lock = threading.Lock()
        self._connected = False
        self.writes: list[bytes] = []

    def connect(self) -> bool:
        self._connected = True
        return True

    def disconnect(self) -> bool:
        self._connected = False
        return True

    def is_connected(self) -> bool:
        return self._connected

    def read(self) -> bytes:
        with self._lock:
            if self._buffer:
                return self._buffer.popleft()
        return b""

    def write(self, data: bytes) -> None:
        if not data:
            return
        self.writes.append(bytes(data))
        for line in _split_command_lines(data.decode("iso-8859-1")):
            parts = _parse_command(line)
            if not parts:
                continue
            try:
                cmd_id = int(parts[0])
            except ValueError:
                continue
            for response_id, response_args in self._handler(cmd_id, parts[1:]):
                self.simulate_receive(_format_command(response_id, response_args).encode("iso-8859-1"))

    def simulate_receive(self, data: bytes) -> None:
        with self._lock:
            self._buffer.append(bytes(data))
        self.data_received(self)

    def dispose(self) -> None:
        self._connected = False


class LoopbackFirmware:
    """Emulates test/integration/firmware/src/main.cpp."""

    def handle(self, cmd_id: int, args: list[str]) -> list[tuple[int, list[str]]]:
        command = LoopbackCommand(cmd_id) if cmd_id in set(item.value for item in LoopbackCommand) else None
        if command == LoopbackCommand.PING:
            return [(LoopbackCommand.PONG, ["pong"])]
        if command == LoopbackCommand.ECHO:
            return [(LoopbackCommand.ECHO_RESULT, [args[0] if args else ""])]
        if command == LoopbackCommand.ADD_FLOATS:
            first = float(args[0])
            second = float(args[1])
            return [(LoopbackCommand.ADD_FLOATS_RESULT, [repr(first + second), repr(first - second)])]
        if command == LoopbackCommand.ECHO_INT:
            return [(LoopbackCommand.ECHO_INT_RESULT, [args[0] if args else "0"])]
        if command == LoopbackCommand.ECHO_BOOL:
            value = 0 if not args else int(args[0])
            return [(LoopbackCommand.ECHO_BOOL_RESULT, ["1" if value else "0"])]
        if command == LoopbackCommand.MULTI_ARGS:
            return [(LoopbackCommand.MULTI_ARGS_RESULT, list(args))]
        if command == LoopbackCommand.ECHO_INT16:
            return [(LoopbackCommand.ECHO_INT16_RESULT, [args[0] if args else "0"])]
        if command == LoopbackCommand.ECHO_DOUBLE:
            return [(LoopbackCommand.ECHO_DOUBLE_RESULT, [args[0] if args else "0"])]
        return [(LoopbackCommand.ERROR, [escaping.escape("Unknown command")])]


class LegacyValueFirmware:
    """Emulates the host-to-embedded value ping/pong behavior of legacy tests."""

    def handle(self, cmd_id: int, args: list[str]) -> list[tuple[int, list[str]]]:
        if cmd_id == LegacyCommand.ARE_YOU_READY:
            return [(LegacyCommand.ACK, [escaping.escape("We are ready")])]
        if cmd_id == LegacyCommand.VALUE_PING:
            return [(LegacyCommand.VALUE_PONG, list(args[1:]))]
        if cmd_id == LegacyCommand.MULTI_VALUE_PING:
            return [(LegacyCommand.MULTI_VALUE_PONG, list(args))]
        return []


def _split_command_lines(value: str) -> list[str]:
    lines: list[str] = []
    current: list[str] = []
    escaped = escaping.IsEscaped()
    for char in value:
        is_escaped = escaped.escaped_char(char)
        current.append(char)
        if char == ";" and not is_escaped:
            lines.append("".join(current))
            current = []
    return lines


def _parse_command(line: str) -> list[str]:
    cleaned = escaping.remove(line.strip("\r\n"), ";", "/")
    return escaping.split(cleaned, ",", "/", remove_empty_entries=True)


def _format_command(cmd_id: int, args: list[str]) -> str:
    values = [str(int(cmd_id))]
    values.extend(args)
    return ",".join(values) + ";"
