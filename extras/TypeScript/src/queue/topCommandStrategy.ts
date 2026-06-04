import { Command } from '../command.js';
import { CommandStrategy } from './commandStrategy.js';
import { ListQueue } from './listQueue.js';

export class TopCommandStrategy<TCommand extends Command = Command> extends CommandStrategy<TCommand> {
  override enqueue(queue: ListQueue<CommandStrategy<TCommand>>): void {
    queue.enqueueFront(this);
  }
}
