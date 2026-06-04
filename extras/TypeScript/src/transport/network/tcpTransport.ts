import { Socket } from 'node:net';
import { concatBytes } from '../../encoding.js';
import { TypedEmitter } from '../../typedEmitter.js';
import { ITransport, TransportEvents } from '../transport.js';

export class TcpTransport implements ITransport {
  readonly dataReceived = new TypedEmitter<TransportEvents>();
  timeout = 1000;

  private socket: Socket | undefined;
  private readonly readChunks: Uint8Array[] = [];

  constructor(
    public readonly host: string,
    public readonly port: number,
    timeoutMs = 1000
  ) {
    this.timeout = timeoutMs;
  }

  async connect(): Promise<boolean> {
    if (this.isConnected()) {
      throw new Error('Already connected.');
    }

    const socket = new Socket();
    socket.setNoDelay(true);
    this.socket = socket;

    return new Promise<boolean>((resolve) => {
      let settled = false;
      const timeout = setTimeout(() => {
        if (settled) {
          return;
        }
        settled = true;
        cleanup();
        socket.destroy();
        this.socket = undefined;
        resolve(false);
      }, this.timeout);

      const cleanup = () => {
        clearTimeout(timeout);
        socket.off('connect', onConnect);
        socket.off('error', onError);
      };

      const onConnect = () => {
        if (settled) {
          return;
        }
        settled = true;
        cleanup();
        this.attachSocketHandlers(socket);
        resolve(true);
      };

      const onError = () => {
        if (settled) {
          return;
        }
        settled = true;
        cleanup();
        socket.destroy();
        this.socket = undefined;
        resolve(false);
      };

      socket.once('connect', onConnect);
      socket.once('error', onError);
      socket.connect({ host: this.host, port: this.port });
    });
  }

  async disconnect(): Promise<boolean> {
    const socket = this.socket;
    this.socket = undefined;
    this.readChunks.length = 0;
    if (socket === undefined) {
      return false;
    }

    return new Promise<boolean>((resolve) => {
      const finish = () => resolve(true);
      if (socket.destroyed) {
        finish();
        return;
      }
      socket.once('close', finish);
      socket.end();
      setTimeout(() => {
        if (!socket.destroyed) {
          socket.destroy();
        }
      }, 50);
    });
  }

  isConnected(): boolean {
    return this.socket !== undefined && !this.socket.destroyed;
  }

  read(): Uint8Array {
    const data = concatBytes(this.readChunks);
    this.readChunks.length = 0;
    return data;
  }

  async write(data: Uint8Array): Promise<void> {
    const socket = this.socket;
    if (socket === undefined || socket.destroyed) {
      return;
    }

    await new Promise<void>((resolve) => {
      socket.write(data, () => resolve());
    });
  }

  dispose(): void {
    void this.disconnect();
    this.dataReceived.clear();
  }

  private attachSocketHandlers(socket: Socket): void {
    socket.on('data', (data: Buffer) => {
      this.readChunks.push(new Uint8Array(data));
      this.dataReceived.emit('data');
    });
    socket.on('close', () => {
      if (this.socket === socket) {
        this.socket = undefined;
      }
    });
    socket.on('error', () => {
      socket.destroy();
    });
  }
}
