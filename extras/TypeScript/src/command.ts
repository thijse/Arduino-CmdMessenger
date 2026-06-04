import { BoardType } from './enums.js';
import { millis } from './timeUtils.js';

export interface CommandContext {
  readonly fieldSeparator: string;
  readonly commandSeparator: string;
  readonly escapeCharacter: string;
  readonly boardType: BoardType;
}

export class Command {
  communicationManager?: CommandContext;
  cmdId = -1;
  timeStamp = millis();
  protected readonly cmdArgs: string[] = [];

  get arguments(): string[] {
    return [...this.cmdArgs];
  }

  get ok(): boolean {
    return this.cmdId >= 0;
  }

  commandString(): string {
    if (this.communicationManager === undefined) {
      throw new Error('CommunicationManager was not set for command.');
    }
    const parts = [String(this.cmdId), ...this.cmdArgs];
    return parts.join(this.communicationManager.fieldSeparator) + this.communicationManager.commandSeparator;
  }
}
