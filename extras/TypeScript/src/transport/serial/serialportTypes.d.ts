declare module 'serialport' {
  import { EventEmitter } from 'node:events';

  export interface SerialPortOpenOptions<TAutoOpen extends boolean> {
    path: string;
    baudRate: number;
    parity?: 'none' | 'even' | 'odd' | 'mark' | 'space';
    dataBits?: 5 | 6 | 7 | 8;
    stopBits?: 1 | 1.5 | 2;
    autoOpen?: TAutoOpen;
  }

  export interface SerialPortSetOptions {
    dtr?: boolean;
    rts?: boolean;
  }

  export interface PortInfo {
    path: string;
    manufacturer?: string;
    serialNumber?: string;
    pnpId?: string;
    locationId?: string;
    productId?: string;
    vendorId?: string;
  }

  export class SerialPort<TAutoOpen extends boolean = true> extends EventEmitter {
    constructor(options: SerialPortOpenOptions<TAutoOpen>);
    readonly isOpen: boolean;
    open(callback: (error: Error | null | undefined) => void): void;
    close(callback?: (error: Error | null | undefined) => void): void;
    write(data: Uint8Array, callback?: (error: Error | null | undefined) => void): boolean;
    drain(callback?: (error: Error | null | undefined) => void): void;
    set(options: SerialPortSetOptions, callback?: (error: Error | null | undefined) => void): void;
    static list(): Promise<PortInfo[]>;
  }
}
