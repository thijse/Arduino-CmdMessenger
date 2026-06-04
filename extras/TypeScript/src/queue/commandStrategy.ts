import { Command } from '../command.js';
import { ListQueue } from './listQueue.js';

export class CommandStrategy<TCommand extends Command = Command> {
  constructor(public readonly command: TCommand) {}

  enqueue(queue: ListQueue<CommandStrategy<TCommand>>): void {
    queue.enqueue(this);
  }

  dequeue(queue: ListQueue<CommandStrategy<TCommand>>): CommandStrategy<TCommand> | undefined {
    return queue.dequeue();
  }
}
