import { Command } from '../command.js';
import { CommandStrategy } from './commandStrategy.js';
import { ListQueue } from './listQueue.js';

export class CollapseCommandStrategy<TCommand extends Command = Command> extends CommandStrategy<TCommand> {
  override enqueue(queue: ListQueue<CommandStrategy<TCommand>>): void {
    const existingIndex = queue.findIndex((item) => item.command.cmdId === this.command.cmdId);
    if (existingIndex >= 0) {
      queue.removeAt(existingIndex);
    }
    queue.enqueue(this);
  }
}
