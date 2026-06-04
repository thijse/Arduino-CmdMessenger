import { describe, expect, test } from 'vitest';
import { BinaryConverter } from '../src/binaryConverter.js';

describe('BinaryConverter', () => {
  test('round-trips integer widths', () => {
    expect(BinaryConverter.toInt16(BinaryConverter.int16ToString(-1234))).toBe(-1234);
    expect(BinaryConverter.toUint16(BinaryConverter.uint16ToString(65535))).toBe(65535);
    expect(BinaryConverter.toInt32(BinaryConverter.int32ToString(-123456))).toBe(-123456);
    expect(BinaryConverter.toUint32(BinaryConverter.uint32ToString(4_000_000_000))).toBe(4_000_000_000);
  });

  test('round-trips float and double', () => {
    expect(BinaryConverter.toFloat(BinaryConverter.floatToString(3.25))).toBeCloseTo(3.25);
    expect(BinaryConverter.toDouble(BinaryConverter.doubleToString(Math.PI))).toBeCloseTo(Math.PI);
  });

  test('escapes reserved bytes in binary strings', () => {
    expect(BinaryConverter.byteToString(','.charCodeAt(0))).toBe('/,');
    expect(BinaryConverter.toByte('/,')).toBe(','.charCodeAt(0));
  });
});
