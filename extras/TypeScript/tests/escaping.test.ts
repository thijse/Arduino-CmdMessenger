import { describe, expect, test } from 'vitest';
import { escape, remove, setEscapeChars, split, unescape } from '../src/escaping.js';

describe('escaping', () => {
  test('escapes reserved characters', () => {
    setEscapeChars(',', ';', '/');
    expect(escape(',')).toBe('/,');
    expect(escape(';')).toBe('/;');
    expect(escape('/')).toBe('//');
    expect(escape(',;/')).toBe('/,/;//');
    expect(escape('\0')).toBe('/\0');
  });

  test('round-trips escaped values', () => {
    setEscapeChars(',', ';', '/');
    for (const value of ['plain', 'a,b', 'a;b', 'a/b', 'cafe\u00e9', '']) {
      expect(unescape(escape(value))).toBe(value);
    }
  });

  test('split respects escaped separators', () => {
    setEscapeChars(',', ';', '/');
    expect(split('a/,b,c', ',', '/')).toEqual(['a/,b', 'c']);
  });

  test('remove skips escaped characters', () => {
    setEscapeChars(',', ';', '/');
    expect(remove('a,b,c', ',', '/')).toBe('abc');
    expect(remove('a/,b', ',', '/')).toBe('a/,b');
  });

  test('trailing escape is dropped on unescape', () => {
    setEscapeChars(',', ';', '/');
    expect(unescape('hello/')).toBe('hello');
  });
});
