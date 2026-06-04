import { BinaryConverter } from './binaryConverter.js';
import { Command } from './command.js';
import { BoardType } from './enums.js';
import { escape } from './escaping.js';

export type CommandArgument = string | number | boolean;

export interface SendCommandOptions {
  args?: readonly CommandArgument[];
  ackCmdId?: number;
  timeout?: number;
}

function isOptions(value: CommandArgument | SendCommandOptions): value is SendCommandOptions {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function numberToText(value: number, boardType: BoardType): string {
  const wireValue = Number.isInteger(value) || boardType === BoardType.Bit32 ? value : Math.fround(value);
  return String(wireValue);
}

export class SendCommand extends Command {
  reqAc = false;
  ackCmdId = 0;
  timeout = 0;

  private readonly lazyArguments: Array<() => void> = [];

  constructor(cmdId: number, ...argsOrOptions: Array<CommandArgument | SendCommandOptions>) {
    super();
    this.cmdId = cmdId;

    if (argsOrOptions.length === 1 && isOptions(argsOrOptions[0] as CommandArgument | SendCommandOptions)) {
      const options = argsOrOptions[0] as SendCommandOptions;
      this.applyOptions(options);
      this.addArguments(options.args ?? []);
      return;
    }

    for (const arg of argsOrOptions as CommandArgument[]) {
      this.addArgument(arg);
    }
  }

  withAck(ackCmdId: number, timeout = 0): this {
    this.reqAc = true;
    this.ackCmdId = ackCmdId;
    this.timeout = timeout;
    return this;
  }

  addArgument(value: CommandArgument | null | undefined): this {
    if (value === null || value === undefined) {
      return this;
    }
    if (typeof value === 'boolean') {
      this.lazyArguments.push(() => this.cmdArgs.push(value ? '1' : '0'));
    } else if (typeof value === 'number') {
      this.lazyArguments.push(() => {
        const boardType = this.communicationManager?.boardType ?? BoardType.Bit16;
        this.cmdArgs.push(numberToText(value, boardType));
      });
    } else {
      this.lazyArguments.push(() => this.cmdArgs.push(value));
    }
    return this;
  }

  addArguments(values: readonly CommandArgument[]): this {
    for (const value of values) {
      this.addArgument(value);
    }
    return this;
  }

  /** Convenience binary argument. Prefer explicit width-specific methods for numeric values. */
  addBinArgument(value: CommandArgument): this {
    if (typeof value === 'boolean') {
      return this.addBinBoolArgument(value);
    }
    if (typeof value === 'string') {
      return this.addBinStringArgument(value);
    }
    if (Number.isInteger(value)) {
      return this.addBinInt32Argument(value);
    }
    return this.addBinDoubleArgument(value);
  }

  addBinStringArgument(value: string): this {
    this.lazyArguments.push(() => this.cmdArgs.push(escape(value, this.protocolChars())));
    return this;
  }

  addBinBoolArgument(value: boolean): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.byteToString(value ? 1 : 0, this.protocolChars())));
    return this;
  }

  addBinByteArgument(value: number): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.byteToString(value, this.protocolChars())));
    return this;
  }

  addBinInt16Argument(value: number): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.int16ToString(value, this.protocolChars())));
    return this;
  }

  addBinUint16Argument(value: number): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.uint16ToString(value, this.protocolChars())));
    return this;
  }

  addBinInt32Argument(value: number): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.int32ToString(value, this.protocolChars())));
    return this;
  }

  addBinUint32Argument(value: number): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.uint32ToString(value, this.protocolChars())));
    return this;
  }

  addBinFloatArgument(value: number): this {
    this.lazyArguments.push(() => this.cmdArgs.push(BinaryConverter.floatToString(value, this.protocolChars())));
    return this;
  }

  addBinDoubleArgument(value: number): this {
    this.lazyArguments.push(() => {
      const boardType = this.communicationManager?.boardType ?? BoardType.Bit16;
      const encoded =
        boardType === BoardType.Bit16
          ? BinaryConverter.floatToString(value, this.protocolChars())
          : BinaryConverter.doubleToString(value, this.protocolChars());
      this.cmdArgs.push(encoded);
    });
    return this;
  }

  initArguments(): void {
    this.cmdArgs.length = 0;
    for (const action of this.lazyArguments) {
      action();
    }
  }

  private applyOptions(options: SendCommandOptions): void {
    if (options.ackCmdId !== undefined) {
      this.withAck(options.ackCmdId, options.timeout ?? 0);
    }
  }

  private protocolChars() {
    return {
      fieldSeparator: this.communicationManager?.fieldSeparator ?? ',',
      commandSeparator: this.communicationManager?.commandSeparator ?? ';',
      escapeCharacter: this.communicationManager?.escapeCharacter ?? '/'
    };
  }

}
