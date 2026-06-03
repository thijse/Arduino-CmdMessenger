"""``ConnectionStorer`` — persists transport connection settings between sessions.

Provides a ``ConnectionStorer`` ABC and a default ``JsonConnectionStorer``
implementation that stores settings as a JSON file on disk.

This abstraction supports serial today and is extensible to network,
Bluetooth, and Wi-Fi transports in the future.
"""
from __future__ import annotations

import json
from abc import ABC, abstractmethod
from pathlib import Path
from typing import Any, Dict, Optional


class ConnectionStorer(ABC):
    """Persists transport connection settings between sessions.

    Mirrors the intent of C#'s settings-persistence pattern.
    """

    @abstractmethod
    def load(self) -> Optional[Dict[str, Any]]:
        """Load stored settings. Returns None if nothing stored."""

    @abstractmethod
    def save(self, settings: Dict[str, Any]) -> None:
        """Persist settings."""

    @abstractmethod
    def clear(self) -> None:
        """Remove stored settings."""


class JsonConnectionStorer(ConnectionStorer):
    """Stores connection settings as a JSON file on disk.

    Default location: ``~/.cmdmessenger/connection.json``

    Args:
        path: Custom file path. If None, uses the default location.
        section: Optional key to scope settings within the JSON file,
            allowing multiple transports to share one file.
    """

    def __init__(
        self,
        path: Optional[Path] = None,
        section: Optional[str] = None,
    ) -> None:
        self._path = path or Path.home() / ".cmdmessenger" / "connection.json"
        self._section = section

    def load(self) -> Optional[Dict[str, Any]]:
        if not self._path.exists():
            return None
        try:
            data = json.loads(self._path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return None
        if self._section:
            return data.get(self._section)
        return data

    def save(self, settings: Dict[str, Any]) -> None:
        self._path.parent.mkdir(parents=True, exist_ok=True)
        if self._section:
            existing: Dict[str, Any] = {}
            if self._path.exists():
                try:
                    existing = json.loads(self._path.read_text(encoding="utf-8"))
                except (json.JSONDecodeError, OSError):
                    existing = {}
            existing[self._section] = settings
            self._path.write_text(json.dumps(existing, indent=2), encoding="utf-8")
        else:
            self._path.write_text(json.dumps(settings, indent=2), encoding="utf-8")

    def clear(self) -> None:
        if not self._path.exists():
            return
        if self._section:
            try:
                data = json.loads(self._path.read_text(encoding="utf-8"))
                data.pop(self._section, None)
                self._path.write_text(json.dumps(data, indent=2), encoding="utf-8")
            except (json.JSONDecodeError, OSError):
                pass
        else:
            self._path.unlink(missing_ok=True)
