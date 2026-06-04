import { CommandContext } from '../command.js';
import { SendQueue } from '../enums.js';
import { SendCommand } from '../sendCommand.js';
import { TypedEmitter } from '../typedEmitter.js';
import { CommandQueue } from './commandQueue.js';
import { CommandStrategy } from './commandStrategy.js';
import { TopCommandStrategy } from './topCommandStrategy.js';

export interface SendCommandExecutor extends CommandContext {
  readonly printLfCr: boolean;
  executeSendCommand(sendCommand: SendCommand, sendQueueState: SendQueue): Promise<unknown>;
  executeSendString(commandString: string, sendQueueState: SendQueue): Promise<unknown>;
}

interface SendCommandQueueEvents {
  newLineSent: (command: SendCommand) => void;
}

export class SendCommandQueue extends CommandQueue<SendCommand> {
  readonly events = new TypedEmitter<SendCommandQueueEvents>();
  maxQueueLength = 5000;
  private sendBuffer = '';
  private commandCount = 0;

  constructor(
    private readonly communicationManager: SendCommandExecutor,
    private readonly sendBufferMaxLength = 62
  ) {
    super();
  }

  override queueCommand(commandStrategy: CommandStrategy<SendCommand>): void {
    if (this.count > this.maxQueueLength) {
      throw new Error(`Send queue length exceeded ${this.maxQueueLength}.`);
    }

    commandStrategy.command.communicationManager = this.communicationManager;
    commandStrategy.command.initArguments();
    commandStrategy.enqueue(this.queue);

    for (const generalStrategy of this.generalStrategies) {
      generalStrategy.onEnqueue();
    }

    this.signalWorker();
  }

  sendCommand(sendCommand: SendCommand): void {
    this.queueCommand(new TopCommandStrategy(sendCommand));
  }

  queueSendCommand(sendCommand: SendCommand): void {
    this.queueCommand(new CommandStrategy(sendCommand));
  }

  protected override async processQueue(): Promise<boolean> {
    await this.sendCommandsFromQueue();
    return !this.isEmpty;
  }

  private async sendCommandsFromQueue(): Promise<void> {
    this.commandCount = 0;
    this.sendBuffer = '';

    while (this.sendBuffer.length < this.sendBufferMaxLength && !this.isEmpty) {
      const commandStrategy = this.queue.peek();
      if (commandStrategy === undefined) {
        break;
      }

      const sendCommand = commandStrategy.command;
      if (sendCommand.reqAc) {
        if (this.commandCount > 0) {
          break;
        }
        await this.sendSingleCommandFromQueue(commandStrategy);
      } else {
        this.addToCommandString(commandStrategy);
        this.events.emit('newLineSent', sendCommand);
      }
    }

    if (this.sendBuffer.length > 0) {
      await this.communicationManager.executeSendString(this.sendBuffer, SendQueue.InFrontQueue);
    }
  }

  private async sendSingleCommandFromQueue(commandStrategy: CommandStrategy<SendCommand>): Promise<void> {
    commandStrategy.dequeue(this.queue);
    for (const generalStrategy of this.generalStrategies) {
      generalStrategy.onDequeue();
    }
    await this.communicationManager.executeSendCommand(commandStrategy.command, SendQueue.InFrontQueue);
  }

  private addToCommandString(commandStrategy: CommandStrategy<SendCommand>): void {
    commandStrategy.dequeue(this.queue);
    for (const generalStrategy of this.generalStrategies) {
      generalStrategy.onDequeue();
    }
    this.commandCount += 1;
    this.sendBuffer += commandStrategy.command.commandString();
    if (this.communicationManager.printLfCr) {
      this.sendBuffer += '\r\n';
    }
  }
}
