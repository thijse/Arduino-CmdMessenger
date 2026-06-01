"""Entry point for ArduinoController web sample."""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from shared import WebForm
from arduino_controller import ArduinoController


def main() -> None:
    static_dir = os.path.join(os.path.dirname(__file__), "static")
    form = WebForm(title="Arduino Controller", port=8080, static_dir=static_dir)

    app = ArduinoController()
    app.setup(form)

    print("Open http://localhost:8080 in your browser")
    try:
        form.start(block=True)
    except KeyboardInterrupt:
        pass
    finally:
        app.exit()


if __name__ == "__main__":
    main()
