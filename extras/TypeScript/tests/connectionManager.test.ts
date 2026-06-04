import { AddressInfo, createServer, Server, Socket } from 'node:net';
import { afterEach, describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { ConnectionManagerMode } from '../src/connectionManager.js';
import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { BoardType } from '../src/enums.js';
import { TcpConnectionManager } from '../src/transport/network/tcpConnectionManager.js';
import { TcpTransport } from '../src/transport/network/tcpTransport.js';

async function listen(server: Server): Promise<number> {
  await new Promise<void>((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve());
  });
  return (server.address() as AddressInfo).port;
}

async function closeServer(server: Server | undefined): Promise<void> {
  if (server === undefined || !server.listening) {
    return;
  }
  await new Promise<void>((resolve) => {
    server.close(() => resolve());
  });
}

describe('ConnectionManager', () => {
  let server: Server | undefined;
  let sockets: Socket[] = [];

  afterEach(async () => {
    for (const socket of sockets) {
      socket.destroy();
    }
    sockets = [];
    await closeServer(server);
    server = undefined;
  });

  test('TcpConnectionManager connects when identify ACK responds', async () => {
    server = createServer((socket) => {
      sockets.push(socket);
      socket.on('data', (data) => {
        if (latin1Decode(new Uint8Array(data)) === '0;') {
          socket.write(latin1Encode('0;'));
        }
      });
    });
    const port = await listen(server);
    const transport = new TcpTransport('127.0.0.1', port, 250);
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    const manager = new TcpConnectionManager(transport, messenger, 0);

    const found = new Promise<void>((resolve) => {
      manager.events.once('connectionFound', resolve);
    });
    manager.startConnectionManager();
    await found;

    expect(manager.connected).toBe(true);
    expect(manager.mode).toBe(ConnectionManagerMode.Wait);
    manager.dispose();
    messenger.dispose();
  });

  test('watchdog requires a unique device id', () => {
    const transport = new TcpTransport('127.0.0.1', 9, 10);
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    const manager = new TcpConnectionManager(transport, messenger, 0);

    expect(() => {
      manager.watchdogEnabled = true;
    }).toThrow("Watchdog can't be enabled without Unique Device ID.");

    manager.dispose();
    messenger.dispose();
  });
});
