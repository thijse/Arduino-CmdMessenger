import { CommandContext } from './command.js';
import { latin1Decode, latin1Encode } from './encoding.js';
import { BoardType, SendQueue } from './enums.js';
import { IsEscaped, remove, split } from './escaping.js';
import { ReceiveCommandQueue } from './queue/receiveCommandQueue.js';
import { ReceivedCommand } from './receivedCommand.js';
import { SendCommand } from './sendCommand.js';
import { millis } from './timeUtils.js';
import { ITransport } from './transport/transport.js';

export class CommunicationManager implements CommandContext {
  printLfCr = false;
  lastLineTimeStamp = 0;
  private buffer = '';

  constructor(
    private readonly transport: ITransport,
    private readonly receiveCommandQueue: ReceiveCommandQueue,
    public readonly boardType: BoardType,
    public readonly commandSeparator = ';',
    public readonly fieldSeparator = ',',
    public readonly escapeCharacter = '/'
  ) {
    this.transport.dataReceived.on('data', this.newDataReceived);
  }

  async connect(): Promise<boolean> {
    if (this.transport.isConnected()) {
      return false;
    }
    return this.transport.connect();
  }

  async disconnect(): Promise<boolean> {
    if (!this.transport.isConnected()) {
      return false;
    }
    return this.transport.disconnect();
  }

  async write(value: string): Promise<void> {
    await this.transport.write(latin1Encode(value));
  }

  async writeLine(value: string): Promise<void> {
    await this.write(`${value}\r\n`);
  }

  async executeSendCommand(sendCommand: SendCommand, sendQueueState: SendQueue): Promise<ReceivedCommand> {
    sendCommand.communicationManager = this;
    sendCommand.initArguments();

    if (!sendCommand.reqAc) {
      await this.writeCommand(sendCommand);
      return this.emptyReceivedCommand();
    }

    this.receiveCommandQueue.suspend();
    try {
      this.receiveCommandQueue.prepareForCmd(sendCommand.ackCmdId, sendQueueState);
      await this.writeCommand(sendCommand);
      const received = await this.receiveCommandQueue.waitForCmd(sendCommand.timeout);
      const ack = received ?? new ReceivedCommand();
      ack.communicationManager = this;
      return ack;
    } finally {
      this.receiveCommandQueue.resume();
    }
  }

  async executeSendString(commandString: string, _sendQueueState: SendQueue): Promise<ReceivedCommand> {
    if (this.printLfCr) {
      await this.writeLine(commandString);
    } else {
      await this.write(commandString);
    }
    return this.emptyReceivedCommand();
  }

  dispose(): void {
    this.transport.dataReceived.off('data', this.newDataReceived);
  }

  private readonly newDataReceived = (): void => {
    this.parseLines();
  };

  private parseLines(): void {
    const data = this.transport.read();
    if (data.length > 0) {
      this.buffer += latin1Decode(data);
    }

    while (true) {
      const currentLine = this.parseLine();
      if (currentLine === '') {
        break;
      }
      this.lastLineTimeStamp = millis();
      this.processLine(currentLine);
    }
  }

  private parseLine(): string {
    if (this.buffer.length === 0) {
      return '';
    }

    const endOfLine = this.findNextEndOfLine();
    if (endOfLine >= 0) {
      const line = this.buffer.slice(0, endOfLine + 1);
      this.buffer = this.buffer.slice(endOfLine + 1);
      return line;
    }

    return '';
  }

  private findNextEndOfLine(): number {
    const escaped = new IsEscaped(this.escapeCharacter);
    for (let i = 0; i < this.buffer.length; i += 1) {
      const char = this.buffer[i] ?? '';
      const currentEscaped = escaped.escapedChar(char);
      if (char === this.commandSeparator && !currentEscaped) {
        return i;
      }
    }
    return -1;
  }

  private processLine(line: string): void {
    const received = this.parseMessage(line);
    received.rawString = line;
    received.timeStamp = this.lastLineTimeStamp;
    this.receiveCommandQueue.queueReceivedCommand(received);
  }

  private parseMessage(line: string): ReceivedCommand {
    const cleaned = remove(line.replace(/[\r\n]+$/u, ''), this.commandSeparator, this.escapeCharacter);
    const args = split(cleaned, this.fieldSeparator, this.escapeCharacter, true);
    const received = new ReceivedCommand(args);
    received.communicationManager = this;
    return received;
  }

  private async writeCommand(sendCommand: SendCommand): Promise<void> {
    if (this.printLfCr) {
      await this.writeLine(sendCommand.commandString());
    } else {
      await this.write(sendCommand.commandString());
    }
  }

  private emptyReceivedCommand(): ReceivedCommand {
    const command = new ReceivedCommand();
    command.communicationManager = this;
    return command;
  }
}
