import { describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { BoardType, ReceiveQueue, SendQueue, UseQueue } from '../src/enums.js';
import { escape, unescape } from '../src/escaping.js';
import { ReceivedCommand } from '../src/receivedCommand.js';
import { SendCommand } from '../src/sendCommand.js';
import { LoopbackTransport } from '../src/transport/loopbackTransport.js';

function flush(): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

describe('CmdMessenger', () => {
  test('dispatches incoming commands to attached callback', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    let received: ReceivedCommand | undefined;
    messenger.attach(1, (command) => {
      received = command;
    });

    transport.feedInput(latin1Encode(`1,${escape('hello, world')};`));
    await flush();

    expect(received?.cmdId).toBe(1);
    expect(unescape(received?.readStringArg() ?? '')).toBe('hello, world');
    messenger.dispose();
  });

  test('direct send writes command without auto-escaping text args', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    await messenger.sendCommand(
      new SendCommand(10, 'a,b'),
      SendQueue.Default,
      ReceiveQueue.Default,
      UseQueue.BypassQueue
    );

    expect(latin1Decode(transport.getWritten())).toBe('10,a,b;');
    messenger.dispose();
  });

  test('awaits matching ack command while receive dispatch is suspended', async () => {
    const transport = new LoopbackTransport({
      echoWrites: false,
      onWrite(data, loopback) {
        const command = latin1Decode(data);
        if (command === '2;') {
          loopback.feedInput(latin1Encode('3,pong;'));
        }
      }
    });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    let callbackFired = false;
    messenger.attach(3, () => {
      callbackFired = true;
    });

    const response = await messenger.sendCommand(new SendCommand(2).withAck(3, 1000));

    expect(response.ok).toBe(true);
    expect(response.cmdId).toBe(3);
    expect(response.readStringArg()).toBe('pong');
    expect(callbackFired).toBe(false);
    messenger.dispose();
  });

  test('returns empty command on ack timeout', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    const response = await messenger.sendCommand(new SendCommand(2).withAck(3, 5));

    expect(response.ok).toBe(false);
    messenger.dispose();
  });
});
