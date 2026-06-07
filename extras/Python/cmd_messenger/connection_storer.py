"""Serial connection storer — port of C# ``ISerialConnectionStorer`` / ``SerialConnectionStorer``.

Provides an abstract base class for persisting
:class:`~cmd_messenger.transport.serial.serial_connection_manager.SerialConnectionManagerSettings`
and a ready-to-use JSON-backed concrete implementation.
"""
from __future__ import annotations

import json
import os
from abc import ABC, abstractmethod
from typing import TYPE_CHECKING, Optional

if TYPE_CHECKING:
    from .transport.serial.serial_connection_manager import SerialConnectionManagerSettings


class SerialConnectionStorer(ABC):
    """Abstract base class for storing/loading serial connection settings.

    Subclass and implement :meth:`load`, :meth:`save`, and :meth:`clear` to
    provide a custom persistence back-end.
    """

    @abstractmethod
    def load(self) -> Optional[SerialConnectionManagerSettings]:
        """Return the previously saved settings, or *None* if none are stored."""

    @abstractmethod
    def save(self, settings: SerialConnectionManagerSettings) -> None:
        """Persist *settings* so they can be recovered via :meth:`load`."""

    @abstractmethod
    def clear(self) -> None:
        """Remove any previously saved settings."""


class JsonSerialConnectionStorer(SerialConnectionStorer):
    """JSON-file backed implementation of :class:`SerialConnectionStorer`.

    Settings are written as a plain JSON object with ``"port"`` and
    ``"baud_rate"`` keys to *file_path* (default:
    ``"serial_connection_settings.json"``).
    """

    def __init__(self, file_path: str = "serial_connection_settings.json") -> None:
        self._file_path = file_path

    # ------------------------------------------------------------------
    # SerialConnectionStorer interface
    # ------------------------------------------------------------------
    def load(self) -> Optional[SerialConnectionManagerSettings]:
        """Load settings from the JSON file.

        Returns *None* if the file does not exist or cannot be parsed.
        """
        # Lazy import to avoid a circular dependency at module load time.
        from .transport.serial.serial_connection_manager import (  # noqa: PLC0415
            SerialConnectionManagerSettings,
        )
        if not os.path.isfile(self._file_path):
            return None
        try:
            with open(self._file_path, "r", encoding="utf-8") as fh:
                data = json.load(fh)
            settings = SerialConnectionManagerSettings(
                port=data.get("port", ""),
                baud_rate=int(data.get("baud_rate", 0)),
            )
            return settings
        except (OSError, ValueError, KeyError):
            return None

    def save(self, settings: SerialConnectionManagerSettings) -> None:
        """Write *settings* to the JSON file, creating it if necessary."""
        data = {
            "port": settings.port,
            "baud_rate": settings.baud_rate,
        }
        with open(self._file_path, "w", encoding="utf-8") as fh:
            json.dump(data, fh, indent=2)

    def clear(self) -> None:
        """Delete the JSON file if it exists."""
        try:
            os.remove(self._file_path)
        except FileNotFoundError:
            pass
