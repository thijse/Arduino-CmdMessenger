import { BinaryConverter } from './binaryConverter.js';
import { Command } from './command.js';
import { latin1Decode } from './encoding.js';
import { BoardType } from './enums.js';

type FormatToken = { binary: boolean; code: string };

function parseInteger(value: string, min: number, max: number): number | null {
  const trimmed = value.trim();
  if (!/^[+-]?\d+$/.test(trimmed)) {
    return null;
  }
  const parsed = Number(trimmed);
  if (!Number.isInteger(parsed) || parsed < min || parsed > max) {
    return null;
  }
  return parsed;
}

function parseFloatText(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

export class ReceivedCommand extends Command implements Iterable<string> {
  rawString = '';
  private parameter = -1;
  private dumped = true;

  constructor(rawArguments?: readonly string[]) {
    super();
    if (rawArguments === undefined || rawArguments.length === 0) {
      return;
    }

    const cmdId = parseInteger(rawArguments[0] ?? '', 0, Number.MAX_SAFE_INTEGER);
    if (cmdId === null) {
      this.cmdId = -1;
      return;
    }

    this.cmdId = cmdId;
    this.cmdArgs.push(...rawArguments.slice(1));
  }

  next(): boolean {
    if (this.dumped) {
      if (this.parameter < this.cmdArgs.length - 1) {
        this.parameter += 1;
        this.dumped = false;
        return true;
      }
      return false;
    }
    return true;
  }

  available(): boolean {
    return this.next();
  }

  [Symbol.iterator](): Iterator<string> {
    return this.arguments[Symbol.iterator]();
  }

  readInt16Arg(): number {
    return this.readInteger(-32768, 32767);
  }

  readUint16Arg(): number {
    return this.readInteger(0, 65535);
  }

  readInt32Arg(): number {
    return this.readInteger(-(2 ** 31), 2 ** 31 - 1);
  }

  readUint32Arg(): number {
    return this.readInteger(0, 2 ** 32 - 1);
  }

  readFloatArg(): number {
    if (this.next()) {
      const parsed = parseFloatText(this.current());
      if (parsed !== null) {
        this.dumped = true;
        return parsed;
      }
    }
    return 0;
  }

  readDoubleArg(): number {
    if (this.communicationManager === undefined) {
      throw new Error('CommunicationManager was not set for command.');
    }
    return this.readFloatArg();
  }

  readStringArg(): string {
    if (this.next()) {
      this.dumped = true;
      return this.current();
    }
    return '';
  }

  readBoolArg(): boolean {
    return this.readInt32Arg() !== 0;
  }

  readCharArg(): string {
    const value = this.readStringArg();
    return value.length > 0 ? value[0] ?? '\0' : '\0';
  }

  readBinInt16Arg(): number {
    return this.readBinaryNumber((value) => BinaryConverter.toInt16(value));
  }

  readBinUint16Arg(): number {
    return this.readBinaryNumber((value) => BinaryConverter.toUint16(value));
  }

  readBinInt32Arg(): number {
    return this.readBinaryNumber((value) => BinaryConverter.toInt32(value));
  }

  readBinUint32Arg(): number {
    return this.readBinaryNumber((value) => BinaryConverter.toUint32(value));
  }

  readBinFloatArg(): number {
    return this.readBinaryNumber((value) => BinaryConverter.toFloat(value));
  }

  readBinDoubleArg(): number {
    if (this.communicationManager === undefined) {
      throw new Error('CommunicationManager was not set for command.');
    }
    return this.readBinaryNumber((value) =>
      this.communicationManager?.boardType === BoardType.Bit16
        ? BinaryConverter.toFloat(value)
        : BinaryConverter.toDouble(value)
    );
  }

  readBinByteArg(): number {
    return this.readBinaryNumber((value) => BinaryConverter.toByte(value));
  }

  readBinStringArg(): string {
    if (this.next()) {
      this.dumped = true;
      return latin1Decode(BinaryConverter.escapedStringToBytes(this.current()));
    }
    return '';
  }

  readBinBoolArg(): boolean {
    return this.readBinByteArg() !== 0;
  }

  read(format: string): unknown[] {
    return this.parseFormat(format).map((token) => this.readToken(token));
  }

  private readInteger(min: number, max: number): number {
    if (this.next()) {
      const parsed = parseInteger(this.current(), min, max);
      if (parsed !== null) {
        this.dumped = true;
        return parsed;
      }
    }
    return 0;
  }

  private readBinaryNumber(reader: (value: string) => number | null): number {
    if (this.next()) {
      const parsed = reader(this.current());
      if (parsed !== null) {
        this.dumped = true;
        return parsed;
      }
    }
    return 0;
  }

  private current(): string {
    return this.cmdArgs[this.parameter] ?? '';
  }

  private parseFormat(format: string): FormatToken[] {
    const tokens: FormatToken[] = [];
    let last: FormatToken | undefined;
    for (let i = 0; i < format.length; i += 1) {
      const char = format[i] ?? '';
      if (char === '*') {
        if (i === format.length - 1) {
          if (last === undefined) {
            throw new Error("Trailing '*' has no preceding format code to repeat.");
          }
          const remaining = this.cmdArgs.length - (this.parameter + (this.dumped ? 1 : 0)) - tokens.length;
          for (let j = 0; j < Math.max(remaining, 0); j += 1) {
            tokens.push(last);
          }
          continue;
        }
        const token = { binary: true, code: format[i + 1] ?? '' };
        tokens.push(token);
        last = token;
        i += 1;
      } else {
        const token = { binary: false, code: char };
        tokens.push(token);
        last = token;
      }
    }
    return tokens;
  }

  private readToken(token: FormatToken): unknown {
    const textReaders: Record<string, () => unknown> = {
      i: () => this.readInt32Arg(),
      I: () => this.readUint32Arg(),
      h: () => this.readInt16Arg(),
      H: () => this.readUint16Arg(),
      b: () => this.readInt16Arg(),
      B: () => this.readUint16Arg(),
      f: () => this.readFloatArg(),
      d: () => this.readDoubleArg(),
      s: () => this.readStringArg(),
      '?': () => this.readBoolArg(),
      c: () => this.readCharArg()
    };
    const binaryReaders: Record<string, () => unknown> = {
      i: () => this.readBinInt32Arg(),
      I: () => this.readBinUint32Arg(),
      h: () => this.readBinInt16Arg(),
      H: () => this.readBinUint16Arg(),
      b: () => this.readBinByteArg(),
      B: () => this.readBinByteArg(),
      f: () => this.readBinFloatArg(),
      d: () => this.readBinDoubleArg(),
      s: () => this.readBinStringArg(),
      '?': () => this.readBinBoolArg()
    };
    const reader = (token.binary ? binaryReaders : textReaders)[token.code];
    if (reader === undefined) {
      throw new Error(`Unknown format code ${token.binary ? '*' : ''}${token.code}`);
    }
    return reader();
  }
}
