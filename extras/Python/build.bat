@echo off
echo === Building Py-CmdMessenger environment ===

REM Optional: override pip index (e.g. set PIP_INDEX_URL=https://pypi.org/simple)
REM Useful when behind a corporate proxy that doesn't mirror all packages.
if defined PIP_INDEX_URL (
    echo Using PIP_INDEX_URL=%PIP_INDEX_URL%
    set PIP_INDEX_ARG=--index-url %PIP_INDEX_URL%
) else (
    set PIP_INDEX_ARG=
)

if not exist "%~dp0.venv" (
    echo Creating virtual environment...
    python -m venv "%~dp0.venv"
) else (
    echo Virtual environment already exists.
)

echo Activating virtual environment...
call "%~dp0.venv\Scripts\activate.bat"

echo Upgrading pip...
python -m pip install %PIP_INDEX_ARG% --upgrade pip

echo Installing requirements...
pip install %PIP_INDEX_ARG% -r "%~dp0requirements.txt"

echo Installing py-cmdmessenger in editable mode...
pip install %PIP_INDEX_ARG% -e "%~dp0."

echo === Build complete ===
