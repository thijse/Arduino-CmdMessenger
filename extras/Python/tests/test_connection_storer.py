"""Tests for :mod:`cmd_messenger.connection_storer`.

Covers :class:`SerialConnectionStorer` (ABC contract) and the JSON-backed
concrete implementation :class:`JsonSerialConnectionStorer`.
"""
from __future__ import annotations

import json
import os
import tempfile

import pytest

from cmd_messenger.connection_storer import JsonSerialConnectionStorer, SerialConnectionStorer
from cmd_messenger.transport.serial.serial_connection_manager import SerialConnectionManagerSettings


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

class _MinimalSerialConnectionStorer(SerialConnectionStorer):
    """Minimal in-memory implementation used to verify the ABC contract."""

    def __init__(self) -> None:
        self._data: SerialConnectionManagerSettings | None = None

    def load(self) -> SerialConnectionManagerSettings | None:
        return self._data

    def save(self, settings: SerialConnectionManagerSettings) -> None:
        self._data = settings

    def clear(self) -> None:
        self._data = None


# ---------------------------------------------------------------------------
# SerialConnectionStorer — ABC contract
# ---------------------------------------------------------------------------

class TestSerialConnectionStorerContract:
    """Verify that :class:`SerialConnectionStorer` enforces the ABC contract."""

    def test_cannot_instantiate_abstract_class(self):
        """SerialConnectionStorer must not be instantiated directly."""
        with pytest.raises(TypeError):
            SerialConnectionStorer()  # type: ignore[abstract]

    def test_minimal_subclass_satisfies_contract(self):
        """A subclass that implements all abstract methods can be instantiated."""
        storer = _MinimalSerialConnectionStorer()
        assert isinstance(storer, SerialConnectionStorer)

    def test_load_returns_none_when_empty(self):
        """load() returns None when no settings have been saved."""
        storer = _MinimalSerialConnectionStorer()
        assert storer.load() is None

    def test_save_and_load_roundtrip(self):
        """save() followed by load() returns equivalent settings."""
        storer = _MinimalSerialConnectionStorer()
        original = SerialConnectionManagerSettings(port="COM3", baud_rate=115200)
        storer.save(original)
        loaded = storer.load()
        assert loaded is not None
        assert loaded.port == "COM3"
        assert loaded.baud_rate == 115200

    def test_clear_removes_saved_settings(self):
        """clear() causes load() to return None."""
        storer = _MinimalSerialConnectionStorer()
        storer.save(SerialConnectionManagerSettings(port="COM1", baud_rate=9600))
        storer.clear()
        assert storer.load() is None


# ---------------------------------------------------------------------------
# JsonSerialConnectionStorer
# ---------------------------------------------------------------------------

class TestJsonSerialConnectionStorer:
    """Tests for the JSON-backed :class:`JsonSerialConnectionStorer`."""

    def setup_method(self):
        # Each test gets its own temp file that is cleaned up afterwards.
        fd, self._path = tempfile.mkstemp(suffix=".json")
        os.close(fd)
        # Start with no file so load() can return None.
        os.remove(self._path)
        self._storer = JsonSerialConnectionStorer(self._path)

    def teardown_method(self):
        try:
            os.remove(self._path)
        except FileNotFoundError:
            pass

    # --- isinstance ---

    def test_is_serial_connection_storer(self):
        """JsonSerialConnectionStorer is a SerialConnectionStorer."""
        assert isinstance(self._storer, SerialConnectionStorer)

    # --- load ---

    def test_load_returns_none_when_file_absent(self):
        """load() returns None when the backing file does not exist."""
        assert self._storer.load() is None

    def test_load_returns_none_for_invalid_json(self):
        """load() returns None if the backing file contains invalid JSON."""
        with open(self._path, "w") as fh:
            fh.write("not valid json {{{{")
        assert self._storer.load() is None

    # --- save ---

    def test_save_creates_file(self):
        """save() creates the backing file."""
        settings = SerialConnectionManagerSettings(port="/dev/ttyUSB0", baud_rate=57600)
        self._storer.save(settings)
        assert os.path.isfile(self._path)

    def test_save_writes_expected_keys(self):
        """save() writes 'port' and 'baud_rate' keys to the JSON file."""
        settings = SerialConnectionManagerSettings(port="/dev/ttyUSB0", baud_rate=57600)
        self._storer.save(settings)
        with open(self._path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        assert data["port"] == "/dev/ttyUSB0"
        assert data["baud_rate"] == 57600

    # --- roundtrip ---

    def test_save_load_roundtrip(self):
        """save() followed by load() returns settings with the same values."""
        original = SerialConnectionManagerSettings(port="COM5", baud_rate=115200)
        self._storer.save(original)
        loaded = self._storer.load()
        assert loaded is not None
        assert loaded.port == "COM5"
        assert loaded.baud_rate == 115200

    def test_save_overwrites_previous_settings(self):
        """A second save() replaces the previously stored settings."""
        self._storer.save(SerialConnectionManagerSettings(port="COM1", baud_rate=9600))
        self._storer.save(SerialConnectionManagerSettings(port="COM2", baud_rate=19200))
        loaded = self._storer.load()
        assert loaded is not None
        assert loaded.port == "COM2"
        assert loaded.baud_rate == 19200

    # --- clear ---

    def test_clear_removes_file(self):
        """clear() deletes the backing JSON file."""
        self._storer.save(SerialConnectionManagerSettings(port="COM3", baud_rate=9600))
        self._storer.clear()
        assert not os.path.isfile(self._path)

    def test_clear_causes_load_to_return_none(self):
        """After clear(), load() returns None."""
        self._storer.save(SerialConnectionManagerSettings(port="COM3", baud_rate=9600))
        self._storer.clear()
        assert self._storer.load() is None

    def test_clear_on_absent_file_does_not_raise(self):
        """clear() is a no-op (no exception) when the file does not exist."""
        self._storer.clear()  # file was never created

    # --- default file path ---

    def test_default_file_path(self):
        """JsonSerialConnectionStorer uses the expected default file name."""
        storer = JsonSerialConnectionStorer()
        assert storer._file_path == "serial_connection_settings.json"
