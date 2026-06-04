import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { escape, IsEscaped, remove, split } from '../src/escaping.js';
import { LoopbackTransport } from '../src/transport/loopbackTransport.js';
import { LoopbackCommand } from './loopbackCommands.js';

type Response = {
  cmdId: number;
  args: string[];
};

export class LoopbackFirmware {
  private buffer = '';

  sendBootAck(transport: LoopbackTransport): void {
    this.send(transport, { cmdId: LoopbackCommand.Acknowledge, args: ['Arduino ready'] });
  }

  handleWrite(data: Uint8Array, transport: LoopbackTransport): void {
    this.buffer += latin1Decode(data);

    while (true) {
      const end = this.findCommandEnd();
      if (end < 0) {
        break;
      }

      const line = this.buffer.slice(0, end + 1);
      this.buffer = this.buffer.slice(end + 1);

      const parts = parseCommand(line);
      const cmdId = Number(parts[0]);
      if (!Number.isInteger(cmdId)) {
        continue;
      }

      for (const response of this.handleCommand(cmdId, parts.slice(1))) {
        this.send(transport, response);
      }
    }
  }

  private handleCommand(cmdId: number, args: string[]): Response[] {
    switch (cmdId) {
      case LoopbackCommand.Ping:
        return [{ cmdId: LoopbackCommand.Pong, args: ['pong'] }];
      case LoopbackCommand.Echo:
        return [{ cmdId: LoopbackCommand.EchoResult, args: [args[0] ?? ''] }];
      case LoopbackCommand.AddFloats:
        return this.addFloats(args);
      case LoopbackCommand.EchoInt:
        return [{ cmdId: LoopbackCommand.EchoIntResult, args: [args[0] ?? '0'] }];
      case LoopbackCommand.EchoBool:
        return [{ cmdId: LoopbackCommand.EchoBoolResult, args: [Number(args[0] ?? 0) === 0 ? '0' : '1'] }];
      case LoopbackCommand.MultiArgs:
        return [{ cmdId: LoopbackCommand.MultiArgsResult, args: [...args] }];
      case LoopbackCommand.EchoInt16:
        return [{ cmdId: LoopbackCommand.EchoInt16Result, args: [args[0] ?? '0'] }];
      case LoopbackCommand.EchoDouble:
        return [{ cmdId: LoopbackCommand.EchoDoubleResult, args: [String(float32Value(Number(args[0] ?? 0)))] }];
      case LoopbackCommand.WhoAmI:
        return [{ cmdId: LoopbackCommand.WhoAmIResult, args: [escape('UNPROVISIONED'), ''] }];
      default:
        return [{ cmdId: LoopbackCommand.Error, args: [escape('Unknown command')] }];
    }
  }

  private addFloats(args: string[]): Response[] {
    const first = Math.fround(Number(args[0] ?? 0));
    const second = Math.fround(Number(args[1] ?? 0));

    return [
      {
        cmdId: LoopbackCommand.AddFloatsResult,
        args: [
          String(Math.fround(first + second)),
          String(Math.fround(first - second))
        ]
      }
    ];
  }

  private findCommandEnd(): number {
    const escaped = new IsEscaped();
    for (let index = 0; index < this.buffer.length; index += 1) {
      const char = this.buffer[index] ?? '';
      const currentEscaped = escaped.escapedChar(char);
      if (char === ';' && !currentEscaped) {
        return index;
      }
    }
    return -1;
  }

  private send(transport: LoopbackTransport, response: Response): void {
    const command = [String(response.cmdId), ...response.args].join(',') + ';';
    transport.feedInput(latin1Encode(command));
  }
}

function parseCommand(line: string): string[] {
  const cleaned = remove(line.replace(/[\r\n]+$/u, ''), ';', '/');
  return split(cleaned, ',', '/', true);
}

function float32Value(value: number): number {
  const buffer = new ArrayBuffer(4);
  const view = new DataView(buffer);
  view.setFloat32(0, value, true);
  return view.getFloat32(0, true);
}
