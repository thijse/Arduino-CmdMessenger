# Py-CmdMessenger

Pythonic port of the C#/VB [CommandMessenger](https://github.com/thijse/Arduino-CmdMessenger)
library for Arduino serial communication.

> Architecture: see [../../plans/architecture-comparison.md](../../plans/architecture-comparison.md)
> Inspired by [PyCmdMessenger](https://github.com/harmsm/PyCmdMessenger) (harmsm).

## Status

🚧 **Alpha** — feature-complete, actively tested.

## Setup

```
build.bat          # Windows
bash build.sh      # macOS / Linux
```

This creates `.venv/`, installs runtime dependencies, and installs the
`py-cmdmessenger` package in editable mode so samples can `import cmd_messenger`.

If you're behind a corporate pip proxy that doesn't mirror all packages, override the
index by setting `PIP_INDEX_URL` first:

```
set PIP_INDEX_URL=https://pypi.org/simple   # Windows
export PIP_INDEX_URL=https://pypi.org/simple # macOS/Linux
build.bat
```

## Structure

| Path | Purpose |
|------|---------|
| `cmd_messenger/` | The library — import as `import cmd_messenger` |
| `cmd_messenger/transport/` | Pluggable transports (serial, network, etc.) |
| `pyproject.toml` | Package metadata (hatchling build backend) |
| `requirements.txt` | Dev environment dependencies |
| `samples/` | Sample applications (see [samples/README.md](samples/README.md)) |
| `samples/shared/` | Sample helpers — `console_utils.py`, `web_form.py` |
| `tests/` | pytest suite (unit + hardware-in-the-loop) |
| `tests/board_discovery.py` | Auto-discovers connected boards (mirrors C# `BoardDiscovery.cs`) |

## Quick example

```python
from cmd_messenger import CmdMessenger, SendCommand, BoardType
from cmd_messenger.transport.serial import SerialTransport, SerialSettings

settings = SerialSettings(port_name="COM6", baud_rate=115200)
transport = SerialTransport(settings)

with CmdMessenger(transport, board_type=BoardType.BIT_16) as messenger:
    messenger.connect()
    messenger.send_command(SendCommand(0, "Hello Arduino"))
```

## Testing

### Unit tests (no hardware needed)

```bash
pytest tests/ -m "not hardware"
```

### Hardware-in-the-loop tests

Requires a board running `test/integration/sketch/src/LoopbackTestRunner.ino`.

Board auto-discovery mirrors C# `BoardDiscovery.cs`:
- **Phase 1:** USB VID:PID matching (Teensy, ESP32-S3)
- **Phase 2:** Serial kWhoAmI query for CH340 boards (Nano, ESP8266)

```bash
# Run all hardware tests (auto-discovers connected boards)
pytest --hardware -m hardware

# Run tests for a specific board
pytest --hardware -k "NANO"
```

Legacy single-board override (skips auto-discovery):
```bash
set CMDMESSENGER_PORT=COM3
pytest --hardware -m hardware
```

## License

MIT — same as the parent C# library. © 2026 Thijs Elenbaas.
