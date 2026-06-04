import { latin1Decode, latin1Encode } from './encoding.js';
import { escape, ProtocolChars, unescape } from './escaping.js';

function bytesToEscapedString(bytes: Uint8Array, chars?: ProtocolChars): string {
  return escape(latin1Decode(bytes), chars);
}

function escapedStringToBytes(value: string): Uint8Array {
  return latin1Encode(unescape(value));
}

function viewForWrite(byteLength: number): { view: DataView; bytes: Uint8Array } {
  const bytes = new Uint8Array(byteLength);
  return { bytes, view: new DataView(bytes.buffer) };
}

function viewForRead(value: string, byteLength: number): DataView | null {
  const bytes = escapedStringToBytes(value);
  if (bytes.length < byteLength) {
    return null;
  }
  return new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
}

export class BinaryConverter {
  static escapedStringToBytes(value: string): Uint8Array {
    return escapedStringToBytes(value);
  }

  static stringToBytes(value: string): Uint8Array {
    return latin1Encode(value);
  }

  static byteToString(value: number, chars?: ProtocolChars): string {
    return bytesToEscapedString(Uint8Array.of(value & 0xff), chars);
  }

  static int16ToString(value: number, chars?: ProtocolChars): string {
    const { view, bytes } = viewForWrite(2);
    view.setInt16(0, value, true);
    return bytesToEscapedString(bytes, chars);
  }

  static uint16ToString(value: number, chars?: ProtocolChars): string {
    const { view, bytes } = viewForWrite(2);
    view.setUint16(0, value, true);
    return bytesToEscapedString(bytes, chars);
  }

  static int32ToString(value: number, chars?: ProtocolChars): string {
    const { view, bytes } = viewForWrite(4);
    view.setInt32(0, value, true);
    return bytesToEscapedString(bytes, chars);
  }

  static uint32ToString(value: number, chars?: ProtocolChars): string {
    const { view, bytes } = viewForWrite(4);
    view.setUint32(0, value, true);
    return bytesToEscapedString(bytes, chars);
  }

  static floatToString(value: number, chars?: ProtocolChars): string {
    const { view, bytes } = viewForWrite(4);
    view.setFloat32(0, value, true);
    return bytesToEscapedString(bytes, chars);
  }

  static doubleToString(value: number, chars?: ProtocolChars): string {
    const { view, bytes } = viewForWrite(8);
    view.setFloat64(0, value, true);
    return bytesToEscapedString(bytes, chars);
  }

  static toByte(value: string): number | null {
    const bytes = escapedStringToBytes(value);
    return bytes.length >= 1 ? bytes[0] ?? 0 : null;
  }

  static toInt16(value: string): number | null {
    return viewForRead(value, 2)?.getInt16(0, true) ?? null;
  }

  static toUint16(value: string): number | null {
    return viewForRead(value, 2)?.getUint16(0, true) ?? null;
  }

  static toInt32(value: string): number | null {
    return viewForRead(value, 4)?.getInt32(0, true) ?? null;
  }

  static toUint32(value: string): number | null {
    return viewForRead(value, 4)?.getUint32(0, true) ?? null;
  }

  static toFloat(value: string): number | null {
    return viewForRead(value, 4)?.getFloat32(0, true) ?? null;
  }

  static toDouble(value: string): number | null {
    return viewForRead(value, 8)?.getFloat64(0, true) ?? null;
  }
}
