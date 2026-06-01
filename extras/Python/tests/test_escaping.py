"""Tests for the escaping utility module.

Port of C# ``EscapingTests.cs``. Tests verify:
1. Each special character is properly escaped on encode
2. Escape → unescape is lossless (round-trip identity)
3. split() respects escaped separators
4. remove() skips escaped characters
5. Custom separator sets work identically
"""
from __future__ import annotations

import pytest

from cmd_messenger import escaping


@pytest.fixture(autouse=True)
def reset_separators():
    """Ensure default separators before every test."""
    escaping.set_escape_chars(",", ";", "/")
    yield
    escaping.set_escape_chars(",", ";", "/")


# =====================================================================
# Escape — plain text unchanged
# =====================================================================
@pytest.mark.parametrize("input_str,expected", [
    ("hello", "hello"),
    ("", ""),
    ("no special chars", "no special chars"),
])
def test_escape_plain_text_unchanged(input_str, expected):
    assert escaping.escape(input_str) == expected


# =====================================================================
# Escape — special characters
# =====================================================================
def test_escape_field_separator():
    assert escaping.escape(",") == "/,"
    assert escaping.escape("a,b") == "a/,b"


def test_escape_command_separator():
    assert escaping.escape(";") == "/;"
    assert escaping.escape("a;b") == "a/;b"


def test_escape_escape_character():
    assert escaping.escape("/") == "//"
    assert escaping.escape("a/b") == "a//b"


def test_escape_null_char():
    assert escaping.escape("\0") == "/\0"


def test_escape_multiple_special_chars():
    # ",;/" → "/,/;//"
    assert escaping.escape(",;/") == "/,/;//"


# =====================================================================
# Round-trip (escape → unescape)
# =====================================================================
@pytest.mark.parametrize("original", [
    "hello",
    "",
    ",",
    ";",
    "/",
    "\0",
    ",;/\0",
    "a,b;c/d\0e",
    "//,,;;",
])
def test_escape_unescape_round_trip(original):
    escaped = escaping.escape(original)
    assert escaping.unescape(escaped) == original


# =====================================================================
# Unescape — specific values
# =====================================================================
@pytest.mark.parametrize("input_str,expected", [
    ("hello", "hello"),
    ("//", "/"),
    ("/,", ","),
    ("/;", ";"),
])
def test_unescape_values(input_str, expected):
    assert escaping.unescape(input_str) == expected


# =====================================================================
# Split
# =====================================================================
def test_split_basic_fields():
    parts = escaping.split("1,hello,42", ",", "/")
    assert parts == ["1", "hello", "42"]


def test_split_escaped_separator_not_split():
    parts = escaping.split("a/,b", ",", "/")
    assert len(parts) == 1
    assert parts[0] == "a/,b"


def test_split_empty_fields_preserved():
    parts = escaping.split("1,,3", ",", "/")
    assert len(parts) == 3
    assert parts[1] == ""


def test_split_remove_empty_entries():
    parts = escaping.split("1,,3", ",", "/", remove_empty_entries=True)
    assert len(parts) == 2
    assert parts == ["1", "3"]


def test_split_empty_string():
    parts = escaping.split("", ",", "/")
    assert len(parts) == 1
    assert parts[0] == ""


def test_split_only_separators():
    parts = escaping.split(",,", ",", "/")
    assert len(parts) == 3
    assert all(p == "" for p in parts)


# =====================================================================
# Remove
# =====================================================================
def test_remove_unescaped_char():
    result = escaping.remove("a,b,c", ",", "/")
    assert result == "abc"


def test_remove_escaped_char_preserved():
    result = escaping.remove("a/,b", ",", "/")
    assert result == "a/,b"


# =====================================================================
# Custom separators
# =====================================================================
def test_custom_separators_round_trip():
    escaping.set_escape_chars("|", "\n", "\\")
    original = "hello|world\nfoo\\bar"
    escaped = escaping.escape(original)
    unescaped = escaping.unescape(escaped)
    assert unescaped == original


# =====================================================================
# Edge cases
# =====================================================================
@pytest.mark.parametrize("input_str", [
    "café",
    "ñoño",
    "über",
    "\u00FF",  # ÿ — max Latin-1 char
])
def test_escape_latin1_chars_round_trip(input_str):
    escaped = escaping.escape(input_str)
    assert escaping.unescape(escaped) == input_str


@pytest.mark.parametrize("input_str", [
    " ",
    "   ",
    "\t",
    "\r\n",
])
def test_escape_whitespace_round_trip(input_str):
    escaped = escaping.escape(input_str)
    assert escaping.unescape(escaped) == input_str


def test_escape_long_string_round_trip():
    input_str = "x" * 10000 + "," + "y" * 10000
    escaped = escaping.escape(input_str)
    assert escaping.unescape(escaped) == input_str


def test_all_special_chars_consecutive_round_trip():
    input_str = ",;/\0,;/\0"
    escaped = escaping.escape(input_str)
    assert escaping.unescape(escaped) == input_str


def test_unescape_trailing_escape_char():
    # Malformed: escape char at end — should not raise
    result = escaping.unescape("hello/")
    # Trailing escape is silently dropped or kept — verify no crash
    assert isinstance(result, str)
