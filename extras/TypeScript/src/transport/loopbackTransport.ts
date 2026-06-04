import { concatBytes } from '../encoding.js';
import { TypedEmitter } from '../typedEmitter.js';
import { ITransport, TransportEvents } from './transport.js';

export interface LoopbackTransportOptions {
  echoWrites?: boolean;
  onWrite?: (data: Uint8Array, transport: LoopbackTransport) => void | Promise<void>;
}

export class LoopbackTransport implements ITransport {
  readonly dataReceived = new TypedEmitter<TransportEvents>();
  private connected = false;
  private readonly readChunks: Uint8Array[] = [];
  private readonly writtenChunks: Uint8Array[] = [];
  private readonly echoWrites: boolean;
  private readonly onWrite: ((data: Uint8Array, transport: LoopbackTransport) => void | Promise<void>) | undefined;

  constructor(options: LoopbackTransportOptions = {}) {
    this.echoWrites = options.echoWrites ?? true;
    this.onWrite = options.onWrite;
  }

  async connect(): Promise<boolean> {
    if (this.connected) {
      return false;
    }
    this.connected = true;
    return true;
  }

  async disconnect(): Promise<boolean> {
    if (!this.connected) {
      return false;
    }
    this.connected = false;
    return true;
  }

  isConnected(): boolean {
    return this.connected;
  }

  read(): Uint8Array {
    const data = concatBytes(this.readChunks);
    this.readChunks.length = 0;
    return data;
  }

  async write(data: Uint8Array): Promise<void> {
    const copy = new Uint8Array(data);
    this.writtenChunks.push(copy);
    if (this.onWrite !== undefined) {
      await this.onWrite(copy, this);
    }
    if (this.echoWrites) {
      this.feedInput(copy);
    }
  }

  feedInput(data: Uint8Array): void {
    this.readChunks.push(new Uint8Array(data));
    this.dataReceived.emit('data');
  }

  getWritten(): Uint8Array {
    return concatBytes(this.writtenChunks);
  }

  clearWritten(): void {
    this.writtenChunks.length = 0;
  }

  dispose(): void {
    void this.disconnect();
    this.readChunks.length = 0;
    this.writtenChunks.length = 0;
    this.dataReceived.clear();
  }
}
