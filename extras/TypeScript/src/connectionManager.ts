import { CmdMessenger } from './cmdMessenger.js';
import { ReceiveQueue, SendQueue, UseQueue } from './enums.js';
import { ReceivedCommand } from './receivedCommand.js';
import { SendCommand } from './sendCommand.js';
import { delay, millis } from './timeUtils.js';
import { TypedEmitter } from './typedEmitter.js';

export enum ConnectionManagerMode {
  Wait = 'wait',
  Connect = 'connect',
  Scan = 'scan',
  Watchdog = 'watchdog'
}

export enum DeviceStatus {
  NotAvailable = 'not_available',
  Available = 'available',
  IdentityMismatch = 'identity_mismatch'
}

export interface ConnectionManagerProgress {
  level: number;
  description: string;
}

interface ConnectionManagerEvents {
  connectionFound: () => void;
  connectionTimeout: () => void;
  progress: (progress: ConnectionManagerProgress) => void;
}

export abstract class ConnectionManager {
  readonly events = new TypedEmitter<ConnectionManagerEvents>();

  connected = false;
  watchdogTimeout = 3000;
  watchdogRetryTimeout = 1500;
  watchdogTries = 3;
  persistentSettings = false;
  deviceScanEnabled = true;

  private running = false;
  private modeValue = ConnectionManagerMode.Wait;
  private watchdogEnabledValue = false;
  private lastCheckTime = 0;
  private nextTimeoutCheck = 0;
  private watchdogTriesUsed = 0;

  protected constructor(
    protected readonly cmdMessenger: CmdMessenger,
    private readonly identifyCommandId = 0,
    private readonly uniqueDeviceId?: string
  ) {
    if (this.uniqueDeviceId !== undefined && this.uniqueDeviceId.length > 0) {
      this.cmdMessenger.attach(this.identifyCommandId, (command) => this.onIdentifyResponse(command));
    }
  }

  get mode(): ConnectionManagerMode {
    return this.modeValue;
  }

  get watchdogEnabled(): boolean {
    return this.watchdogEnabledValue;
  }

  set watchdogEnabled(value: boolean) {
    if (value && (this.uniqueDeviceId === undefined || this.uniqueDeviceId.length === 0)) {
      throw new Error("Watchdog can't be enabled without Unique Device ID.");
    }
    this.watchdogEnabledValue = value;
  }

  startConnectionManager(): void {
    if (!this.running) {
      this.running = true;
      void this.runLoop();
    }

    if (this.deviceScanEnabled) {
      this.startScan();
    } else {
      this.startConnect();
    }
  }

  stopConnectionManager(): void {
    this.running = false;
    this.modeValue = ConnectionManagerMode.Wait;
    void this.disconnect();
  }

  dispose(): void {
    this.stopConnectionManager();
    this.events.clear();
  }

  protected abstract doWorkConnect(): Promise<void>;
  protected abstract doWorkScan(): Promise<void>;

  protected async arduinoAvailable(timeoutMs: number, tries = 1): Promise<DeviceStatus> {
    for (let i = 1; i <= tries; i += 1) {
      if (tries > 1) {
        this.log(3, `Polling Arduino, try # ${i}`);
      }

      const challenge = new SendCommand(this.identifyCommandId).withAck(this.identifyCommandId, timeoutMs);
      const response = await this.cmdMessenger.sendCommand(
        challenge,
        SendQueue.InFrontQueue,
        ReceiveQueue.Default,
        UseQueue.BypassQueue
      );

      if (response.ok && this.uniqueDeviceId !== undefined && this.uniqueDeviceId.length > 0) {
        return this.validateDeviceUniqueId(response) ? DeviceStatus.Available : DeviceStatus.IdentityMismatch;
      }

      if (response.ok) {
        return DeviceStatus.Available;
      }
    }

    return DeviceStatus.NotAvailable;
  }

  protected connectionFoundEvent(): void {
    this.modeValue = ConnectionManagerMode.Wait;
    if (this.watchdogEnabledValue) {
      this.startWatchdog();
    }
    this.events.emit('connectionFound');
  }

  protected async connectionTimeoutEvent(): Promise<void> {
    this.modeValue = ConnectionManagerMode.Wait;
    await this.disconnect();
    this.events.emit('connectionTimeout');

    if (this.watchdogEnabledValue) {
      this.stopWatchdog();
      if (this.deviceScanEnabled) {
        this.startScan();
      } else {
        this.startConnect();
      }
    }
  }

  protected log(level: number, description: string): void {
    this.events.emit('progress', { level, description });
  }

  protected startScan(): void {
    if (this.modeValue !== ConnectionManagerMode.Scan && !this.connected) {
      this.log(1, 'Starting device scan.');
      this.modeValue = ConnectionManagerMode.Scan;
    }
  }

  protected stopScan(): void {
    if (this.modeValue === ConnectionManagerMode.Scan) {
      this.log(1, 'Stopping device scan.');
      this.modeValue = ConnectionManagerMode.Wait;
    }
  }

  protected startConnect(): void {
    if (this.modeValue !== ConnectionManagerMode.Connect && !this.connected) {
      this.log(1, 'Start connecting to device.');
      this.modeValue = ConnectionManagerMode.Connect;
    }
  }

  protected stopConnect(): void {
    if (this.modeValue === ConnectionManagerMode.Connect) {
      this.log(1, 'Stop connecting to device.');
      this.modeValue = ConnectionManagerMode.Wait;
    }
  }

  private async runLoop(): Promise<void> {
    while (this.running) {
      try {
        switch (this.modeValue) {
          case ConnectionManagerMode.Connect:
            await this.doWorkConnect();
            break;
          case ConnectionManagerMode.Scan:
            await this.doWorkScan();
            break;
          case ConnectionManagerMode.Watchdog:
            await this.doWorkWatchdog();
            break;
          case ConnectionManagerMode.Wait:
            break;
        }
      } catch (error) {
        this.log(2, error instanceof Error ? error.message : String(error));
      }
      await delay(100);
    }
  }

  private onIdentifyResponse(responseCommand: ReceivedCommand): void {
    if (responseCommand.ok && this.uniqueDeviceId !== undefined && this.uniqueDeviceId.length > 0) {
      this.validateDeviceUniqueId(responseCommand);
    }
  }

  private validateDeviceUniqueId(responseCommand: ReceivedCommand): boolean {
    const valid = this.uniqueDeviceId === responseCommand.readStringArg();
    if (!valid) {
      this.log(3, 'Invalid device response. Device ID mismatch.');
    }
    return valid;
  }

  private async doWorkWatchdog(): Promise<void> {
    const lastLineTimeStamp = this.cmdMessenger.lastReceivedCommandTimeStamp;
    const current = millis();

    if (current < this.nextTimeoutCheck) {
      return;
    }

    if (lastLineTimeStamp >= this.lastCheckTime) {
      this.log(3, 'Successful watchdog response.');
      this.lastCheckTime = current;
      this.nextTimeoutCheck = this.lastCheckTime + this.watchdogTimeout;
      this.watchdogTriesUsed = 0;
      return;
    }

    if (this.watchdogTriesUsed >= this.watchdogTries) {
      this.log(2, `Watchdog received no response after final try #${this.watchdogTries}`);
      this.watchdogTriesUsed = 0;
      this.modeValue = ConnectionManagerMode.Wait;
      await this.connectionTimeoutEvent();
      return;
    }

    await this.cmdMessenger.sendCommand(new SendCommand(this.identifyCommandId));
    this.watchdogTriesUsed += 1;
    this.lastCheckTime = current;
    this.nextTimeoutCheck = this.lastCheckTime + this.watchdogRetryTimeout;
    this.log(
      3,
      this.watchdogTriesUsed === 1
        ? `Watchdog detected no communication for ${this.watchdogTimeout / 1000}s, asking for response`
        : `Watchdog received no response, performing try #${this.watchdogTriesUsed}`
    );
  }

  private startWatchdog(): void {
    if (this.modeValue !== ConnectionManagerMode.Watchdog && this.connected) {
      this.log(1, 'Starting Watchdog.');
      this.lastCheckTime = millis();
      this.nextTimeoutCheck = this.lastCheckTime + this.watchdogTimeout;
      this.watchdogTriesUsed = 0;
      this.modeValue = ConnectionManagerMode.Watchdog;
    }
  }

  private stopWatchdog(): void {
    if (this.modeValue === ConnectionManagerMode.Watchdog) {
      this.log(1, 'Stopping Watchdog.');
      this.modeValue = ConnectionManagerMode.Wait;
    }
  }

  private async disconnect(): Promise<boolean> {
    if (!this.connected) {
      return true;
    }
    this.connected = false;
    return this.cmdMessenger.disconnect();
  }
}
