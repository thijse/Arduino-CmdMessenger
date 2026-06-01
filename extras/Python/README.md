# Py-CmdMessenger

Pythonic port of the C#/VB [CommandMessenger](https://github.com/thijse/Arduino-CmdMessenger)
library for Arduino serial communication.

> Architecture: see [../../plans/Python/architecture.md](../../plans/Python/architecture.md)
> Inspired by [PyCmdMessenger](https://github.com/harmsm/PyCmdMessenger) (harmsm).

## Status

🚧 **Alpha** — under active development.

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
| `pyproject.toml` | Package metadata (hatchling build backend) |
| `requirements.txt` | Dev environment dependencies |
| `1_receive/` … `9_temperature_control/` | Sample applications (see architecture doc) |
| `shared/` | Sample helpers — `console_utils.py`, `web_form.py` |

## Quick example

```python
from cmd_messenger import CmdMessenger, SendCommand, BoardType
from cmd_messenger.transport.serial import SerialTransport, SerialSettings

transport = SerialTransport()
transport.current_serial_settings = SerialSettings(port_name="COM6", baud_rate=115200)

with CmdMessenger(transport, board_type=BoardType.BIT_16) as messenger:
    messenger.connect()
    messenger.send_command(SendCommand(0, "Hello Arduino"))
```

## License

MIT — same as the parent C# library. © 2026 Thijs Elenbaas.
