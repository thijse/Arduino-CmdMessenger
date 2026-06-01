"""``Logger`` — Python adaptation of the C# ``Logger`` static class.

The C# version writes ISO-8859-1 encoded bytes to a single file stream. In
Python we lean on the standard :mod:`logging` module while preserving the
shape of the C# API (``open``/``close``/``log``/``log_line``/``IsEnabled``).

When the file-based logger is opened, a :class:`logging.FileHandler` is
attached to the module-level logger ``cmd_messenger``. Callers can also use
``logging.getLogger("cmd_messenger")`` directly to consume messages.
"""
from __future__ import annotations

import logging
from typing import Optional

_logger = logging.getLogger("cmd_messenger")

_is_enabled: bool = True
_is_open: bool = False
_log_file_name: Optional[str] = None
_file_handler: Optional[logging.FileHandler] = None
_direct_flush: bool = False


def is_enabled() -> bool:
    return _is_enabled


def set_enabled(value: bool) -> None:
    global _is_enabled
    _is_enabled = value


def is_open() -> bool:
    return _is_open


def get_log_file_name() -> Optional[str]:
    return _log_file_name


def get_direct_flush() -> bool:
    return _direct_flush


def set_direct_flush(value: bool) -> None:
    global _direct_flush
    _direct_flush = value


def open(log_file_name: Optional[str] = None) -> bool:  # noqa: A001 - mirrors C# name
    """Open (or reopen) the log file. Returns True on success."""
    global _is_open, _log_file_name, _file_handler
    if log_file_name is None:
        log_file_name = _log_file_name
    if log_file_name is None:
        return False
    if _is_open and _log_file_name == log_file_name:
        return True
    close()
    try:
        handler = logging.FileHandler(log_file_name, mode="w", encoding="iso-8859-1")
        handler.setFormatter(logging.Formatter("%(message)s"))
        _logger.addHandler(handler)
        _logger.setLevel(logging.DEBUG)
    except OSError:
        return False
    _file_handler = handler
    _log_file_name = log_file_name
    _is_open = True
    return True


def close() -> None:
    global _is_open, _file_handler
    if not _is_open:
        return
    try:
        if _file_handler is not None:
            _logger.removeHandler(_file_handler)
            _file_handler.close()
    except OSError:
        pass
    _file_handler = None
    _is_open = False


def log(message: str) -> None:
    """Write ``message`` to the log without a trailing newline."""
    if not _is_enabled or not _is_open:
        return
    # FileHandler adds its own newline per record; emit a single-line record.
    _logger.info(message.rstrip("\r\n"))
    if _direct_flush and _file_handler is not None:
        _file_handler.flush()


def log_line(message: str) -> None:
    """Write ``message`` plus a newline."""
    log(message)
