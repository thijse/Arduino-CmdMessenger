import { CmdMessenger } from '../../cmdMessenger.js';
import { ConnectionManager, ConnectionManagerMode, DeviceStatus } from '../../connectionManager.js';
import { getPortNames, getSupportedBaudRates, commonBaudRates } from './serialUtils.js';
import { SerialTransport } from './serialTransport.js';

enum ScanType {
  None = 0,
  Quick = 1,
  Thorough = 2
}

export class SerialConnectionManager extends ConnectionManager {
  availableSerialPorts: string[] = [];
  deviceScanBaudRateSelection = true;
  private scanType = ScanType.None;

  constructor(
    private readonly serialTransport: SerialTransport,
    cmdMessenger: CmdMessenger,
    watchdogCommandId = 0,
    uniqueDeviceId?: string
  ) {
    super(cmdMessenger, watchdogCommandId, uniqueDeviceId);
  }

  protected override startScan(): void {
    super.startScan();
    if (this.mode === ConnectionManagerMode.Scan) {
      this.scanType = ScanType.None;
      void this.updateAvailablePorts();
    }
  }

  protected override async doWorkConnect(): Promise<void> {
    const activeConnection = (await this.tryConnection()) === DeviceStatus.Available;
    if (activeConnection) {
      this.connectionFoundEvent();
    }
  }

  protected override async doWorkScan(): Promise<void> {
    let activeConnection = false;

    if (this.scanType === ScanType.None) {
      activeConnection = (await this.tryConnection()) === DeviceStatus.Available;
      this.scanType = ScanType.Quick;
    } else if (this.scanType === ScanType.Quick) {
      activeConnection = await this.quickScan();
      this.scanType = ScanType.Thorough;
    } else {
      activeConnection = await this.thoroughScan();
      this.scanType = ScanType.Quick;
    }

    if (activeConnection) {
      this.connectionFoundEvent();
    }
  }

  private async tryConnection(portName?: string, baudRate?: number): Promise<DeviceStatus> {
    const settings = this.serialTransport.currentSerialSettings;
    const oldPort = settings.portName;
    const oldBaud = settings.baudRate;

    if (portName !== undefined) {
      settings.portName = portName;
    }
    if (baudRate !== undefined) {
      settings.baudRate = baudRate;
    }

    if (!settings.isValid()) {
      settings.portName = oldPort;
      settings.baudRate = oldBaud;
      return DeviceStatus.NotAvailable;
    }

    this.connected = false;
    this.log(1, `Trying serial port ${settings.portName} at ${settings.baudRate} bauds.`);

    if (!(await this.serialTransport.connect())) {
      return DeviceStatus.NotAvailable;
    }

    const status = await this.arduinoAvailable(settings.timeout + 250);
    this.connected = status === DeviceStatus.Available;

    if (this.connected) {
      this.log(1, `Connected to serial port ${settings.portName} at ${settings.baudRate} bauds.`);
    } else {
      await this.serialTransport.disconnect();
    }

    return status;
  }

  private async quickScan(): Promise<boolean> {
    this.log(3, 'Performing quick scan.');
    await this.updateAvailablePorts();

    for (const portName of this.availableSerialPorts) {
      const baudRates = this.candidateBaudRates(portName);
      for (const baudRate of baudRates) {
        if (this.mode !== ConnectionManagerMode.Scan) {
          return false;
        }
        const status = await this.tryConnection(portName, baudRate);
        if (status === DeviceStatus.Available) {
          return true;
        }
        if (status === DeviceStatus.IdentityMismatch) {
          break;
        }
      }
    }

    return false;
  }

  private async thoroughScan(): Promise<boolean> {
    this.log(1, 'Performing thorough scan.');
    await this.updateAvailablePorts();

    for (const portName of this.availableSerialPorts) {
      const baudRates = getSupportedBaudRates(portName);
      for (const baudRate of baudRates) {
        if (this.mode !== ConnectionManagerMode.Scan) {
          return false;
        }
        const status = await this.tryConnection(portName, baudRate);
        if (status === DeviceStatus.Available) {
          return true;
        }
        if (status === DeviceStatus.IdentityMismatch) {
          break;
        }
      }
    }

    return false;
  }

  private candidateBaudRates(portName: string): number[] {
    if (!this.deviceScanBaudRateSelection) {
      return [this.serialTransport.currentSerialSettings.baudRate];
    }
    const supported = new Set(getSupportedBaudRates(portName));
    return commonBaudRates.filter((baudRate) => supported.has(baudRate));
  }

  private async updateAvailablePorts(): Promise<void> {
    this.availableSerialPorts = await getPortNames();
  }
}
