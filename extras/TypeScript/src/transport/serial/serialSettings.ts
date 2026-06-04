export type SerialParity = 'none' | 'even' | 'odd' | 'mark' | 'space';
export type SerialStopBits = 1 | 1.5 | 2;
export type SerialDataBits = 5 | 6 | 7 | 8;

export interface SerialSettingsOptions {
  portName?: string;
  baudRate?: number;
  parity?: SerialParity;
  dataBits?: SerialDataBits;
  stopBits?: SerialStopBits;
  dtrEnable?: boolean;
  rtsEnable?: boolean;
  timeout?: number;
}

export class SerialSettings {
  portName: string;
  baudRate: number;
  parity: SerialParity;
  dataBits: SerialDataBits;
  stopBits: SerialStopBits;
  dtrEnable: boolean;
  rtsEnable: boolean;
  timeout: number;

  constructor(options: SerialSettingsOptions = {}) {
    this.portName = options.portName ?? '';
    this.baudRate = options.baudRate ?? 9600;
    this.parity = options.parity ?? 'none';
    this.dataBits = options.dataBits ?? 8;
    this.stopBits = options.stopBits ?? 1;
    this.dtrEnable = options.dtrEnable ?? false;
    this.rtsEnable = options.rtsEnable ?? false;
    this.timeout = options.timeout ?? 500;
  }

  isValid(): boolean {
    return this.portName.length > 0 && this.baudRate > 0 && [5, 6, 7, 8].includes(this.dataBits);
  }
}
