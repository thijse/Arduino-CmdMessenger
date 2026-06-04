import { millis } from '../timeUtils.js';
import { GeneralStrategy } from './generalStrategy.js';

export class StaleGeneralStrategy extends GeneralStrategy {
  constructor(public commandTimeout = 1000) {
    super();
  }

  override onDequeue(): void {
    if (this.commandQueue === undefined) {
      return;
    }
    while (!this.commandQueue.isEmpty) {
      const item = this.commandQueue.peek();
      if (item === undefined || millis() - item.command.timeStamp <= this.commandTimeout) {
        return;
      }
      this.commandQueue.dequeue();
    }
  }
}
