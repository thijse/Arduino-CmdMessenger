import { SendQueue } from './enums.js';
import { EventWaiter, WaitState } from './eventWaiter.js';
import { ReceivedCommand } from './receivedCommand.js';

export class ReceivedCommandSignal {
  private cmdIdToMatch = -1;
  private sendQueueState = SendQueue.Default;
  private receivedCommand: ReceivedCommand | null = null;
  private readonly waiter = new EventWaiter();

  prepareForWait(cmdId: number, sendQueueState: SendQueue): void {
    this.receivedCommand = null;
    this.cmdIdToMatch = cmdId;
    this.sendQueueState = sendQueueState;
    this.waiter.reset();
  }

  async waitForCmd(timeoutMs: number): Promise<ReceivedCommand | null> {
    const state = await this.waiter.waitOne(timeoutMs);
    return state === WaitState.TimeOut ? null : this.receivedCommand;
  }

  processCommand(receivedCommand: ReceivedCommand): boolean {
    if (receivedCommand.cmdId === this.cmdIdToMatch) {
      this.receivedCommand = receivedCommand;
      this.waiter.set();
      return false;
    }
    return this.sendQueueState !== SendQueue.ClearQueue;
  }
}
