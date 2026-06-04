import { SendQueue } from '../enums.js';
import { ReceivedCommand } from '../receivedCommand.js';
import { ReceivedCommandSignal } from '../receivedCommandSignal.js';
import { TypedEmitter } from '../typedEmitter.js';
import { CommandQueue } from './commandQueue.js';
import { CommandStrategy } from './commandStrategy.js';

export type HandleReceivedCommand = (command: ReceivedCommand) => void;

interface ReceiveCommandQueueEvents {
  newLineReceived: (command: ReceivedCommand) => void;
}

export class ReceiveCommandQueue extends CommandQueue<ReceivedCommand> {
  readonly events = new TypedEmitter<ReceiveCommandQueueEvents>();
  private readonly receivedCommandSignal = new ReceivedCommandSignal();

  constructor(private readonly receivedCommandHandler: HandleReceivedCommand) {
    super();
  }

  override queueCommand(commandStrategy: CommandStrategy<ReceivedCommand>): void {
    if (this.isSuspended) {
      const addToQueue = this.receivedCommandSignal.processCommand(commandStrategy.command);
      if (!addToQueue) {
        return;
      }
    }

    this.queue.enqueue(commandStrategy);
    for (const generalStrategy of this.generalStrategies) {
      generalStrategy.onEnqueue();
    }

    if (!this.isSuspended) {
      this.signalWorker();
      this.events.emit('newLineReceived', commandStrategy.command);
    }
  }

  queueReceivedCommand(receivedCommand: ReceivedCommand): void {
    this.queueCommand(new CommandStrategy(receivedCommand));
  }

  dequeueCommand(): ReceivedCommand | undefined {
    return this.dequeueCommandInternal();
  }

  prepareForCmd(cmdId: number, sendQueueState: SendQueue): void {
    this.receivedCommandSignal.prepareForWait(cmdId, sendQueueState);
  }

  waitForCmd(timeoutMs: number): Promise<ReceivedCommand | null> {
    return this.receivedCommandSignal.waitForCmd(timeoutMs);
  }

  protected override processQueue(): boolean {
    const command = this.dequeueCommandInternal();
    const hasMoreWork = !this.isEmpty;
    if (command !== undefined) {
      this.receivedCommandHandler(command);
    }
    return hasMoreWork;
  }

  private dequeueCommandInternal(): ReceivedCommand | undefined {
    if (this.isEmpty) {
      return undefined;
    }
    for (const generalStrategy of this.generalStrategies) {
      generalStrategy.onDequeue();
    }
    return this.queue.dequeue()?.command;
  }
}
