import { TypedEmitter } from '../typedEmitter.js';

export interface TransportEvents {
  data: () => void;
}

export interface ITransport {
  readonly dataReceived: TypedEmitter<TransportEvents>;

  connect(): Promise<boolean>;
  disconnect(): Promise<boolean>;
  isConnected(): boolean;
  read(): Uint8Array;
  write(data: Uint8Array): Promise<void>;
  dispose(): void;
}
