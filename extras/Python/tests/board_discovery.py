"""Board discovery for hardware-in-the-loop tests.

Mirrors ``extras/CSharp/Tests/CommandMessenger.IntegrationTests/BoardDiscovery.cs``.

Discovers provisioned CmdMessenger boards by querying kWhoAmI on all
available serial ports. Results are cached for the lifetime of the process.

Discovery order: first attempt USB VID:PID matching (fast, no DTR reset),
then fall back to serial query for any unmatched ports.
"""
from __future__ import annotations

import os
import re
import sys
import time
from functools import lru_cache
from typing import Dict, Optional, Tuple

import serial
import serial.tools.list_ports

_K_WHO_AM_I = 18


@lru_cache(maxsize=1)
def discover() -> Dict[str, str]:
    """Discover all connected provisioned boards.

    Returns a dict mapping model name (e.g. "NANO", "ESP32S3") to port name.
    Cached after first call.
    """
    result: Dict[str, str] = {}
    matched: set[str] = set()

    # Phase 1: USB VID:PID matching (fast, no DTR reset)
    ports = serial.tools.list_ports.comports()
    for port_info in ports:
        port_name = port_info.device

        # Teensy: VID:PID 16C0:0483
        if port_info.vid == 0x16C0 and port_info.pid == 0x0483:
            result["TEENSY"] = port_name
            matched.add(port_name)
            continue

        # CP210x (ESP32-S3 in our setup): VID:PID 10C4:EA60
        if port_info.vid == 0x10C4 and port_info.pid == 0xEA60:
            result["ESP32S3"] = port_name
            matched.add(port_name)
            continue

    # Phase 2: For CH340 ports (Nano + ESP8266 share VID:PID), query via serial
    for port_info in ports:
        port_name = port_info.device
        if port_name in matched:
            continue
        try:
            model, device_id = _query_identity(port_name)
            if model and model != "UNPROVISIONED":
                result[model] = port_name
        except Exception:
            # Port busy, not a CmdMessenger board, or timeout — skip
            pass

    return result


def find_port(model: str) -> Optional[str]:
    """Returns the COM port for a given board model (e.g. "NANO", "ESP32S3").

    Returns None if the board is not connected.
    """
    return discover().get(model.upper())


def all_boards() -> Dict[str, str]:
    """Returns all discovered boards as a model→port dictionary."""
    return discover()


def _query_identity(port_name: str) -> Tuple[Optional[str], Optional[str]]:
    """Open port, send kWhoAmI, parse response "19,MODEL,ID;"."""
    sp = serial.Serial(
        port=port_name,
        baudrate=115200,
        parity=serial.PARITY_NONE,
        bytesize=serial.EIGHTBITS,
        stopbits=serial.STOPBITS_ONE,
        timeout=0.1,
        write_timeout=0.5,
    )
    sp.dtr = True
    sp.rts = True

    try:
        sp.open() if not sp.is_open else None

        # Wait for board to boot (DTR toggle causes reset on AVR)
        time.sleep(2.5)
        sp.reset_input_buffer()

        # Send kWhoAmI
        sp.write(b"18;\n")
        time.sleep(0.6)

        # Read response
        response = b""
        deadline = time.time() + 2.0
        while time.time() < deadline:
            chunk = sp.read(256)
            if chunk:
                response += chunk
                if b";" in response:
                    break
            else:
                time.sleep(0.05)

    finally:
        sp.close()
        # Let the board settle after we close the port
        time.sleep(0.2)

    # Parse "19,MODEL,ID;"
    raw = response.decode("latin-1", errors="replace").strip().rstrip(";")
    parts = raw.split(",")
    if len(parts) >= 3 and parts[0].strip() == "19":
        return (parts[1].strip(), parts[2].strip())

    return (None, None)
