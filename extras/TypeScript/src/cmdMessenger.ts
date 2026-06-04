import { CommunicationManager } from './communicationManager.js';
import { BoardType, ReceiveQueue, SendQueue, UseQueue } from './enums.js';
import { CommandStrategy } from './queue/commandStrategy.js';
import { GeneralStrategy } from './queue/generalStrategy.js';
import { ReceiveCommandQueue } from './queue/receiveCommandQueue.js';
import { SendCommandQueue } from './queue/sendCommandQueue.js';
import { ReceivedCommand } from './receivedCommand.js';
import { SendCommand } from './sendCommand.js';
import { delay } from './timeUtils.js';
import { TypedEmitter } from './typedEmitter.js';
import { ITransport } from './transport/transport.js';

export type MessengerCallback = (cmd: ReceivedCommand) => void;

interface CmdMessengerEvents {
  newLineReceived: (cmd: ReceivedCommand) => void;
  newLineSent: (cmd: SendCommand) => void;
}

export class CmdMessenger {
  readonly events = new TypedEmitter<CmdMessengerEvents>();
  private readonly receiveCommandQueue: ReceiveCommandQueue;
  private readonly sendCommandQueue: SendCommandQueue;
  private readonly communicationManager: CommunicationManager;
  private readonly callbackList = new Map<number, MessengerCallback>();
  private defaultCallback: MessengerCallback | undefined;

  constructor(
    transport: ITransport,
    boardType = BoardType.Bit16,
    fieldSeparator = ',',
    commandSeparator = ';',
    escapeCharacter = '/',
    sendBufferMaxLength = 60
  ) {
    this.receiveCommandQueue = new ReceiveCommandQueue((command) => this.handleMessage(command));
    this.communicationManager = new CommunicationManager(
      transport,
      this.receiveCommandQueue,
      boardType,
      commandSeparator,
      fieldSeparator,
      escapeCharacter
    );
    this.sendCommandQueue = new SendCommandQueue(this.communicationManager, sendBufferMaxLength);

    this.receiveCommandQueue.events.on('newLineReceived', (command) => {
      this.events.emit('newLineReceived', command);
    });
    this.sendCommandQueue.events.on('newLineSent', (command) => {
      this.events.emit('newLineSent', command);
    });

    this.receiveCommandQueue.start();
    this.sendCommandQueue.start();
  }

  get printLfCr(): boolean {
    return this.communicationManager.printLfCr;
  }

  set printLfCr(value: boolean) {
    this.communicationManager.printLfCr = value;
  }

  get lastReceivedCommandTimeStamp(): number {
    return this.communicationManager.lastLineTimeStamp;
  }

  connect(): Promise<boolean> {
    return this.communicationManager.connect();
  }

  disconnect(): Promise<boolean> {
    return this.communicationManager.disconnect();
  }

  attach(callback: MessengerCallback): void;
  attach(cmdId: number, callback: MessengerCallback): void;
  attach(cmdIdOrCallback: number | MessengerCallback, callback?: MessengerCallback): void {
    if (callback === undefined) {
      this.defaultCallback = cmdIdOrCallback as MessengerCallback;
      return;
    }
    this.callbackList.set(Number(cmdIdOrCallback), callback);
  }

  async sendCommand(
    sendCommand: SendCommand,
    sendQueueState = SendQueue.InFrontQueue,
    receiveQueueState = ReceiveQueue.Default,
    useQueue = UseQueue.UseQueue
  ): Promise<ReceivedCommand> {
    const synchronizedSend = sendCommand.reqAc || useQueue === UseQueue.BypassQueue;

    if (sendCommand.reqAc && receiveQueueState === ReceiveQueue.Default) {
      receiveQueueState = ReceiveQueue.WaitForEmptyQueue;
    }

    if (sendQueueState === SendQueue.ClearQueue) {
      this.receiveCommandQueue.clear();
    }

    if (receiveQueueState === ReceiveQueue.ClearQueue) {
      this.sendCommandQueue.clear();
    }

    if (
      sendQueueState === SendQueue.WaitForEmptyQueue ||
      (synchronizedSend && sendQueueState === SendQueue.AtEndQueue)
    ) {
      await this.waitUntilEmpty(this.sendCommandQueue);
    }

    if (receiveQueueState === ReceiveQueue.WaitForEmptyQueue) {
      await this.waitUntilEmpty(this.receiveCommandQueue);
    }

    if (synchronizedSend) {
      return this.sendCommandSync(sendCommand, sendQueueState);
    }

    if (sendQueueState !== SendQueue.AtEndQueue) {
      this.sendCommandQueue.sendCommand(sendCommand);
    } else {
      this.sendCommandQueue.queueSendCommand(sendCommand);
    }

    return this.emptyReceivedCommand();
  }

  async sendCommandSync(sendCommand: SendCommand, sendQueueState = SendQueue.InFrontQueue): Promise<ReceivedCommand> {
    const result = await this.communicationManager.executeSendCommand(sendCommand, sendQueueState);
    this.events.emit('newLineSent', sendCommand);
    return result;
  }

  sendCommandDirect(sendCommand: SendCommand, sendQueueState = SendQueue.InFrontQueue): Promise<ReceivedCommand> {
    return this.sendCommandSync(sendCommand, sendQueueState);
  }

  queueCommand(command: SendCommand | CommandStrategy<SendCommand>): void {
    if (command instanceof CommandStrategy) {
      this.sendCommandQueue.queueCommand(command);
    } else {
      this.sendCommandQueue.queueSendCommand(command);
    }
  }

  addSendCommandStrategy(strategy: GeneralStrategy): void {
    this.sendCommandQueue.addGeneralStrategy(strategy);
  }

  addReceiveCommandStrategy(strategy: GeneralStrategy): void {
    this.receiveCommandQueue.addGeneralStrategy(strategy);
  }

  clearSendQueue(): void {
    this.sendCommandQueue.clear();
  }

  clearReceiveQueue(): void {
    this.receiveCommandQueue.clear();
  }

  dispose(): void {
    this.communicationManager.dispose();
    this.sendCommandQueue.dispose();
    this.receiveCommandQueue.dispose();
    this.events.clear();
  }

  private handleMessage(receivedCommand: ReceivedCommand): void {
    let callback: MessengerCallback | undefined;
    let commandForCallback = receivedCommand;

    if (receivedCommand.ok) {
      callback = this.callbackList.get(receivedCommand.cmdId) ?? this.defaultCallback;
    } else {
      commandForCallback = this.emptyReceivedCommand();
    }

    if (callback !== undefined) {
      callback(commandForCallback);
    }
  }

  private emptyReceivedCommand(): ReceivedCommand {
    const command = new ReceivedCommand();
    command.communicationManager = this.communicationManager;
    return command;
  }

  private async waitUntilEmpty(queue: { readonly isEmpty: boolean }): Promise<void> {
    while (!queue.isEmpty) {
      await delay(1);
    }
  }
}
