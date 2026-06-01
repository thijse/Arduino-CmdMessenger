#!/usr/bin/env bash
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Building Py-CmdMessenger environment ==="

# Optional: override pip index (e.g. export PIP_INDEX_URL=https://pypi.org/simple)
if [ -n "$PIP_INDEX_URL" ]; then
    echo "Using PIP_INDEX_URL=$PIP_INDEX_URL"
    PIP_INDEX_ARG="--index-url $PIP_INDEX_URL"
else
    PIP_INDEX_ARG=""
fi

if [ ! -d "$SCRIPT_DIR/.venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv "$SCRIPT_DIR/.venv"
else
    echo "Virtual environment already exists."
fi

echo "Activating virtual environment..."
source "$SCRIPT_DIR/.venv/bin/activate"

echo "Upgrading pip..."
python -m pip install $PIP_INDEX_ARG --upgrade pip

echo "Installing requirements..."
pip install $PIP_INDEX_ARG -r "$SCRIPT_DIR/requirements.txt"

echo "Installing py-cmdmessenger in editable mode..."
pip install $PIP_INDEX_ARG -e "$SCRIPT_DIR/."

echo "=== Build complete ==="
