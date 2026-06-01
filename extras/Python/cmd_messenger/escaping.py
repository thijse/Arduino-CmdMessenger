"""Escape / unescape helpers for the CmdMessenger wire protocol.

Direct port of ``CommandMessenger/Escaped.cs``. Two public surfaces:

* :class:`IsEscaped` — small stateful helper tracking whether the **current**
  character in a stream is escaped (i.e. preceded by an unpaired escape char).
* Module-level functions :func:`escape`, :func:`unescape`, :func:`split`,
  :func:`remove`, plus :func:`set_escape_chars` to override the separators.

The default characters match the C# defaults:
    field separator    = ','
    command separator  = ';'
    escape character   = '/'

These are module-level (matching the C# ``static`` fields) so all parts of the
library agree on the active escape configuration.
"""
from __future__ import annotations

from typing import List

# Module-level state — mirrors C# private static fields.
_field_separator: str = ","
_command_separator: str = ";"
_escape_character: str = "/"


def get_escape_character() -> str:
    """Return the currently configured escape character."""
    return _escape_character


def get_field_separator() -> str:
    """Return the currently configured field separator."""
    return _field_separator


def get_command_separator() -> str:
    """Return the currently configured command separator."""
    return _command_separator


def set_escape_chars(field_separator: str, command_separator: str, escape_character: str) -> None:
    """Override the field / command / escape characters (mirrors ``Escaping.EscapeChars``)."""
    global _field_separator, _command_separator, _escape_character
    _field_separator = field_separator
    _command_separator = command_separator
    _escape_character = escape_character


class IsEscaped:
    """Stateful per-stream helper — port of C# ``IsEscaped``.

    Create a new instance for every independent string you scan.
    Call :meth:`escaped_char` for each character in order; it returns ``True``
    if that character is escaped (i.e. immediately preceded by an unpaired
    escape character).
    """

    def __init__(self) -> None:
        self._last_char: str = "\0"

    def escaped_char(self, curr_char: str) -> bool:
        """Return whether ``curr_char`` is escaped, advancing the state."""
        escaped = self._last_char == _escape_character
        self._last_char = curr_char
        # Special case: the escape char has itself been escaped; reset so the
        # next character is not treated as escaped.
        if self._last_char == _escape_character and escaped:
            self._last_char = "\0"
        return escaped


def remove(input_str: str, remove_char: str, escape_char: str) -> str:
    """Remove every occurrence of ``remove_char`` unless escaped.

    Note: ``escape_char`` is accepted for signature parity with C# but the
    underlying :class:`IsEscaped` uses the module-level escape character.
    """
    output_chars: List[str] = []
    escaped = IsEscaped()
    for ch in input_str:
        is_escaped = escaped.escaped_char(ch)
        if ch != remove_char or is_escaped:
            output_chars.append(ch)
    return "".join(output_chars)


def split(input_str: str, separator: str, escape_character: str,
          remove_empty_entries: bool = False) -> List[str]:
    """Split on ``separator`` unless it is escaped by ``escape_character``.

    Direct port of ``Escaping.Split``. ``escape_character`` is the *literal*
    char used to escape (not the module default), matching the C# signature.
    """
    word_chars: List[str] = []
    result: List[str] = []
    i = 0
    n = len(input_str)
    while i < n:
        t = input_str[i]
        if t == separator:
            result.append("".join(word_chars))
            word_chars = []
        else:
            if t == escape_character:
                word_chars.append(t)
                if i < n - 1:
                    i += 1
                    t = input_str[i]
            word_chars.append(t)
        i += 1
    result.append("".join(word_chars))
    if remove_empty_entries:
        result = [w for w in result if w != ""]
    return result


def escape(input_str: str) -> str:
    """Escape the four reserved characters (escape, field, command, ``\\0``).

    The escape character is escaped **first** so subsequent insertions of
    escape chars don't get double-escaped.
    """
    escape_chars = (_escape_character, _field_separator, _command_separator, "\0")
    out = input_str
    for c in escape_chars:
        out = out.replace(c, _escape_character + c)
    return out


def unescape(input_str: str) -> str:
    """Inverse of :func:`escape` — drop an escape character before any literal."""
    output_chars: List[str] = []
    n = len(input_str)
    i = 0
    while i < n:
        if input_str[i] == _escape_character:
            i += 1
            if i >= n:
                break
        output_chars.append(input_str[i])
        i += 1
    return "".join(output_chars)
