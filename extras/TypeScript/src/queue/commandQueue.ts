import { Command } from '../command.js';
import { CommandStrategy } from './commandStrategy.js';
import { GeneralStrategy } from './generalStrategy.js';
import { ListQueue } from './listQueue.js';

export abstract class CommandQueue<TCommand extends Command = Command> {
  protected readonly queue = new ListQueue<CommandStrategy<TCommand>>();
  protected readonly generalStrategies: GeneralStrategy[] = [];
  private running = false;
  private suspended = false;
  private processing = false;
  private signalPending = false;

  get isRunning(): boolean {
    return this.running;
  }

  get isSuspended(): boolean {
    return this.suspended;
  }

  get count(): number {
    return this.queue.count;
  }

  get isEmpty(): boolean {
    return this.queue.isEmpty;
  }

  clear(): void {
    this.queue.clear();
  }

  addGeneralStrategy(generalStrategy: GeneralStrategy): void {
    generalStrategy.commandQueue = this.queue as ListQueue<CommandStrategy>;
    this.generalStrategies.push(generalStrategy);
  }

  abstract queueCommand(commandStrategy: CommandStrategy<TCommand>): void;

  start(): void {
    this.running = true;
    this.signalWorker();
  }

  stop(): void {
    this.running = false;
    this.clear();
  }

  suspend(): void {
    this.suspended = true;
  }

  resume(): void {
    this.suspended = false;
    this.signalWorker();
  }

  dispose(): void {
    this.stop();
  }

  protected signalWorker(): void {
    if (!this.running || this.suspended || this.processing || this.signalPending) {
      return;
    }
    this.signalPending = true;
    queueMicrotask(() => {
      this.signalPending = false;
      void this.drain();
    });
  }

  protected abstract processQueue(): Promise<boolean> | boolean;

  private async drain(): Promise<void> {
    if (this.processing || !this.running || this.suspended) {
      return;
    }
    this.processing = true;
    try {
      while (this.running && !this.suspended) {
        const hasMore = await this.processQueue();
        if (!hasMore) {
          break;
        }
      }
    } finally {
      this.processing = false;
    }
    if (this.running && !this.suspended && !this.isEmpty) {
      this.signalWorker();
    }
  }
}
