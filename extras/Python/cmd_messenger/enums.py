"""Enumerations — port of C# ``SendQueue``, ``ReceiveQueue``, ``UseQueue``, ``BoardType``."""
from __future__ import annotations

from enum import Enum, auto


class SendQueue(Enum):
    """How a send command is added to the send queue (mirrors C# ``SendQueue``)."""

    DEFAULT = auto()
    IN_FRONT_QUEUE = auto()
    AT_END_QUEUE = auto()
    WAIT_FOR_EMPTY_QUEUE = auto()
    CLEAR_QUEUE = auto()


class ReceiveQueue(Enum):
    """How receive-queue behaviour is controlled (mirrors C# ``ReceiveQueue``)."""

    DEFAULT = auto()
    WAIT_FOR_EMPTY_QUEUE = auto()
    CLEAR_QUEUE = auto()


class UseQueue(Enum):
    """Whether to use the queue or bypass it (mirrors C# ``UseQueue``)."""

    USE_QUEUE = auto()
    BYPASS_QUEUE = auto()


class BoardType(Enum):
    """Target Arduino word width.

    On 16-bit boards (UNO, Mega) ``double`` is the same as ``float``, so doubles
    are demoted to floats on the wire.
    """

    BIT_16 = auto()
    BIT_32 = auto()
