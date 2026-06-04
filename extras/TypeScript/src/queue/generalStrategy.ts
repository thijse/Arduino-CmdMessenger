import { CommandStrategy } from './commandStrategy.js';
import { ListQueue } from './listQueue.js';

export class GeneralStrategy {
  commandQueue?: ListQueue<CommandStrategy>;

  onEnqueue(): void {
    // Extension hook.
  }

  onDequeue(): void {
    // Extension hook.
  }
}
