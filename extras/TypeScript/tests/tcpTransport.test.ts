import { AddressInfo, createServer, Server, Socket } from 'node:net';
import { afterEach, describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { BoardType } from '../src/enums.js';
import { SendCommand } from '../src/sendCommand.js';
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

function waitForTransportData(transport: TcpTransport): Promise<void> {
  return new Promise((resolve) => {
    transport.dataReceived.once('data', resolve);
  });
}

describe('TcpTransport', () => {
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

  test('writes to and reads from a TCP server', async () => {
    server = createServer((socket) => {
      sockets.push(socket);
      socket.on('data', (data) => socket.write(data));
    });
    const port = await listen(server);
    const transport = new TcpTransport('127.0.0.1', port);

    expect(await transport.connect()).toBe(true);
    const dataReceived = waitForTransportData(transport);
    await transport.write(latin1Encode('hello'));
    await dataReceived;

    expect(latin1Decode(transport.read())).toBe('hello');
    expect(await transport.disconnect()).toBe(true);
  });

  test('works with CmdMessenger ACK flow', async () => {
    server = createServer((socket) => {
      sockets.push(socket);
      socket.on('data', (data) => {
        if (latin1Decode(new Uint8Array(data)) === '2;') {
          socket.write(latin1Encode('3,pong;'));
        }
      });
    });
    const port = await listen(server);
    const transport = new TcpTransport('127.0.0.1', port);
    const messenger = new CmdMessenger(transport, BoardType.Bit16);

    expect(await messenger.connect()).toBe(true);
    const response = await messenger.sendCommand(new SendCommand(2).withAck(3, 1000));

    expect(response.ok).toBe(true);
    expect(response.cmdId).toBe(3);
    expect(response.readStringArg()).toBe('pong');
    messenger.dispose();
  });
});
