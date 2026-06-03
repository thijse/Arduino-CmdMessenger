"""Tests for the ConnectionStorer abstraction."""
from __future__ import annotations

import json
from pathlib import Path

import pytest

from cmd_messenger.connection_storer import ConnectionStorer, JsonConnectionStorer


class TestJsonConnectionStorer:
    """Tests for the default JSON file-based storer."""

    def test_save_and_load(self, tmp_path: Path) -> None:
        path = tmp_path / "settings.json"
        storer = JsonConnectionStorer(path=path)

        storer.save({"port_name": "COM3", "baud_rate": 115200})
        loaded = storer.load()

        assert loaded == {"port_name": "COM3", "baud_rate": 115200}

    def test_load_returns_none_when_no_file(self, tmp_path: Path) -> None:
        path = tmp_path / "nonexistent.json"
        storer = JsonConnectionStorer(path=path)

        assert storer.load() is None

    def test_clear_removes_file(self, tmp_path: Path) -> None:
        path = tmp_path / "settings.json"
        storer = JsonConnectionStorer(path=path)
        storer.save({"port_name": "COM3"})

        storer.clear()

        assert storer.load() is None
        assert not path.exists()

    def test_section_scoping(self, tmp_path: Path) -> None:
        path = tmp_path / "settings.json"
        serial_storer = JsonConnectionStorer(path=path, section="serial")
        network_storer = JsonConnectionStorer(path=path, section="network")

        serial_storer.save({"port_name": "COM3", "baud_rate": 115200})
        network_storer.save({"host": "192.168.1.10", "port": 8080})

        assert serial_storer.load() == {"port_name": "COM3", "baud_rate": 115200}
        assert network_storer.load() == {"host": "192.168.1.10", "port": 8080}

    def test_clear_section_preserves_others(self, tmp_path: Path) -> None:
        path = tmp_path / "settings.json"
        serial_storer = JsonConnectionStorer(path=path, section="serial")
        network_storer = JsonConnectionStorer(path=path, section="network")

        serial_storer.save({"port_name": "COM3"})
        network_storer.save({"host": "10.0.0.1"})

        serial_storer.clear()

        assert serial_storer.load() is None
        assert network_storer.load() == {"host": "10.0.0.1"}

    def test_creates_parent_directories(self, tmp_path: Path) -> None:
        path = tmp_path / "sub" / "deep" / "settings.json"
        storer = JsonConnectionStorer(path=path)

        storer.save({"port_name": "COM6"})

        assert path.exists()
        assert storer.load() == {"port_name": "COM6"}

    def test_handles_corrupt_json_gracefully(self, tmp_path: Path) -> None:
        path = tmp_path / "settings.json"
        path.write_text("not valid json {{{", encoding="utf-8")

        storer = JsonConnectionStorer(path=path)
        assert storer.load() is None

    def test_overwrite_existing_settings(self, tmp_path: Path) -> None:
        path = tmp_path / "settings.json"
        storer = JsonConnectionStorer(path=path)

        storer.save({"port_name": "COM3"})
        storer.save({"port_name": "COM6", "baud_rate": 9600})

        assert storer.load() == {"port_name": "COM6", "baud_rate": 9600}
