import type { SerialPort as SerialPortInstance } from 'serialport';
import { concatBytes } from '../../encoding.js';
import { TypedEmitter } from '../../typedEmitter.js';
import { ITransport, TransportEvents } from '../transport.js';
import { SerialSettings, SerialSettingsOptions } from './serialSettings.js';

type SerialPortConstructor = typeof SerialPortInstance;

async function loadSerialPort(): Promise<SerialPortConstructor> {
  try {
    const module = await import('serialport');
    return module.SerialPort;
  } catch (error) {
    throw new Error(
      "SerialTransport requires the optional 'serialport' package. Install it with `npm install serialport`.",
      { cause: error }
    );
  }
}

export class SerialTransport implements ITransport {
  readonly dataReceived = new TypedEmitter<TransportEvents>();
  currentSerialSettings: SerialSettings;

  private port: SerialPortInstance<false> | undefined;
  private readonly readChunks: Uint8Array[] = [];

  constructor(settings?: SerialSettings | SerialSettingsOptions) {
    this.currentSerialSettings =
      settings instanceof SerialSettings ? settings : new SerialSettings(settings);
  }

  async connect(): Promise<boolean> {
    if (!this.currentSerialSettings.isValid()) {
      throw new Error('Unable to open connection - serial settings invalid.');
    }
    if (this.isConnected()) {
      throw new Error('Serial port is already opened.');
    }

    const SerialPort = await loadSerialPort();
    const settings = this.currentSerialSettings;
    const port = new SerialPort({
      path: settings.portName,
      baudRate: settings.baudRate,
      parity: settings.parity,
      dataBits: settings.dataBits,
      stopBits: settings.stopBits,
      autoOpen: false
    });

    this.port = port;

    return new Promise<boolean>((resolve) => {
      let settled = false;
      const finish = (connected: boolean) => {
        if (settled) {
          return;
        }
        settled = true;
        clearTimeout(timer);
        resolve(connected);
      };
      const timer = setTimeout(() => {
        if (this.port === port) {
          this.port = undefined;
        }
        if (port.isOpen) {
          port.close();
        }
        finish(false);
      }, settings.timeout);

      port.open((error) => {
        if (settled) {
          if (port.isOpen) {
            port.close();
          }
          return;
        }
        if (error != null) {
          this.port = undefined;
          finish(false);
          return;
        }

        port.on('data', (data: Buffer) => {
          this.readChunks.push(new Uint8Array(data));
          this.dataReceived.emit('data');
        });
        port.on('error', () => {
          void this.disconnect();
        });
        port.on('close', () => {
          if (this.port === port) {
            this.port = undefined;
          }
        });

        port.set({ dtr: settings.dtrEnable, rts: settings.rtsEnable }, (setError) => {
          if (setError != null) {
            if (this.port === port) {
              this.port = undefined;
            }
            port.close();
            finish(false);
            return;
          }
          finish(true);
        });
      });
    });
  }

  async disconnect(): Promise<boolean> {
    const port = this.port;
    this.port = undefined;
    this.readChunks.length = 0;
    if (port === undefined || !port.isOpen) {
      return false;
    }

    return new Promise<boolean>((resolve) => {
      let settled = false;
      const finish = (closed: boolean) => {
        if (settled) {
          return;
        }
        settled = true;
        clearTimeout(timer);
        resolve(closed);
      };
      const timer = setTimeout(() => finish(false), this.currentSerialSettings.timeout);
      port.close(() => finish(true));
    });
  }

  isConnected(): boolean {
    return this.port !== undefined && this.port.isOpen;
  }

  read(): Uint8Array {
    const data = concatBytes(this.readChunks);
    this.readChunks.length = 0;
    return data;
  }

  async write(data: Uint8Array): Promise<void> {
    const port = this.port;
    if (port === undefined || !port.isOpen) {
      return;
    }

    await new Promise<void>((resolve) => {
      port.write(data, () => {
        port.drain(() => resolve());
      });
    });
  }

  dispose(): void {
    void this.disconnect();
    this.dataReceived.clear();
  }
}
