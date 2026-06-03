"""Shared pytest configuration and fixtures for the CmdMessenger test suite.

Hardware-dependent tests (host-to-embedded) are marked with ``@pytest.mark.hardware``
and are SKIPPED by default. To run them, pass ``--hardware`` on the CLI:

    pytest --hardware -k hardware

Environment variables for hardware tests:
    CMDMESSENGER_PORT   - serial port (e.g. COM3, /dev/ttyACM0)
    CMDMSG_HW_PORT      - C#-compatible serial port alias
    CMDMESSENGER_BAUD   - baud rate (default 115200)

NOTE: Embedded-only tests (tests that run purely on the Arduino without any host
involvement) are NOT included in this Python test suite. Those tests are already
handled by the existing C#/C++ test infrastructure in ``extras/CSharp/CommandMessengerTests/``
and ``test/integration/sketch/``.
"""
from __future__ import annotations

import os

import pytest


def pytest_addoption(parser):
    parser.addoption(
        "--hardware",
        action="store_true",
        default=False,
        help="Run hardware-dependent tests (requires Arduino connected via serial).",
    )
    parser.addoption(
        "--firmware-exe",
        default=None,
        help="Path to the native loopback firmware executable for integration tests.",
    )


def pytest_configure(config):
    config.addinivalue_line("markers", "hardware: requires real Arduino hardware")
    config.addinivalue_line("markers", "integration: requires native firmware subprocess")


def pytest_collection_modifyitems(config, items):
    if not config.getoption("--hardware"):
        skip_hw = pytest.mark.skip(reason="needs --hardware option and connected Arduino")
        for item in items:
            if "hardware" in item.keywords:
                item.add_marker(skip_hw)

    # Integration tests need the firmware exe
    firmware_exe = _find_firmware_exe(config)
    if firmware_exe is None:
        skip_int = pytest.mark.skip(
            reason="native firmware not built (run: pio run -e native in test/integration/firmware/)"
        )
        for item in items:
            if "integration" in item.keywords:
                item.add_marker(skip_int)


def _find_firmware_exe(config) -> str | None:
    """Locate the native firmware executable."""
    # Explicit CLI option
    explicit = config.getoption("--firmware-exe")
    if explicit and os.path.isfile(explicit):
        return explicit

    # Walk up from this file to find repo root
    here = os.path.dirname(os.path.abspath(__file__))
    for _ in range(10):
        candidate = os.path.join(
            here, "test", "integration", "firmware",
            ".pio", "build", "native",
            "program.exe" if os.name == "nt" else "program",
        )
        if os.path.isfile(candidate):
            return candidate
        parent = os.path.dirname(here)
        if parent == here:
            break
        here = parent

    return None


@pytest.fixture
def firmware_exe(request):
    """Fixture that provides the path to the native firmware executable."""
    exe = _find_firmware_exe(request.config)
    if exe is None:
        pytest.skip("native firmware not built")
    return exe


@pytest.fixture
def serial_port(request):
    """Fixture that provides the serial port for hardware tests."""
    port = os.environ.get("CMDMESSENGER_PORT") or os.environ.get("CMDMSG_HW_PORT")
    if not port:
        pytest.skip("CMDMESSENGER_PORT or CMDMSG_HW_PORT not set")
    return port


@pytest.fixture
def serial_baud():
    """Fixture that provides the baud rate for hardware tests."""
    return int(os.environ.get("CMDMESSENGER_BAUD", "115200"))
