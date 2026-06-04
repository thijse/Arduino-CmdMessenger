import { CmdMessenger } from '../../cmdMessenger.js';
import { ConnectionManager, DeviceStatus } from '../../connectionManager.js';
import { TcpTransport } from './tcpTransport.js';

export class TcpConnectionManager extends ConnectionManager {
  constructor(
    private readonly tcpTransport: TcpTransport,
    cmdMessenger: CmdMessenger,
    identifyCommandId = 0,
    uniqueDeviceId?: string
  ) {
    super(cmdMessenger, identifyCommandId, uniqueDeviceId);
    this.deviceScanEnabled = false;
  }

  protected override async doWorkConnect(): Promise<void> {
    const activeConnection = (await this.tryConnection()) === DeviceStatus.Available;
    if (activeConnection) {
      this.connectionFoundEvent();
    }
  }

  protected override async doWorkScan(): Promise<void> {
    await this.doWorkConnect();
  }

  private async tryConnection(): Promise<DeviceStatus> {
    this.connected = false;
    this.log(1, `Trying TCP endpoint ${this.tcpTransport.host}:${this.tcpTransport.port}.`);

    if (!(await this.tcpTransport.connect())) {
      return DeviceStatus.NotAvailable;
    }

    const status = await this.arduinoAvailable(this.tcpTransport.timeout + 250);
    this.connected = status === DeviceStatus.Available;

    if (this.connected) {
      this.log(1, `Connected to ${this.tcpTransport.host}:${this.tcpTransport.port}.`);
    } else {
      await this.tcpTransport.disconnect();
    }

    return status;
  }
}
