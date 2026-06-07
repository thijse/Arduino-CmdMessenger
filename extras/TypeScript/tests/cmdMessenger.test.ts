import { describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { BoardType, ReceiveQueue, SendQueue, UseQueue } from '../src/enums.js';
import { escape, unescape } from '../src/escaping.js';
import { CollapseCommandStrategy } from '../src/queue/collapseCommandStrategy.js';
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

  test('escaped field separator in argument is parsed correctly', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    let received: ReceivedCommand | undefined;
    messenger.attach(1, (command) => {
      received = command;
    });

    // Feed a command where the argument contains an escaped comma: hello/,world
    transport.feedInput(latin1Encode('1,hello/,world;'));
    await flush();

    expect(received?.cmdId).toBe(1);
    // The raw arg still has the escape sequence; unescape yields the original string
    expect(unescape(received?.readStringArg() ?? '')).toBe('hello,world');
    messenger.dispose();
  });

  test('multiple commands in one buffer all processed', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    const received: number[] = [];
    messenger.attach(1, () => received.push(1));
    messenger.attach(2, () => received.push(2));

    // Feed two complete commands in a single chunk
    transport.feedInput(latin1Encode('1,first;2,second;'));
    await flush();

    expect(received).toEqual([1, 2]);
    messenger.dispose();
  });

  test('partial data is buffered until complete', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    let fireCount = 0;
    messenger.attach(1, () => { fireCount += 1; });

    // Feed first half of command — no semicolon yet, so nothing fires
    transport.feedInput(latin1Encode('1,hel'));
    await flush();
    expect(fireCount).toBe(0);

    // Feed second half including terminator
    transport.feedInput(latin1Encode('lo;'));
    await flush();
    expect(fireCount).toBe(1);

    messenger.dispose();
  });

  test('empty argument through pipeline', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    // When we send a command with an empty string arg it goes on the wire as "10,;"
    await messenger.sendCommand(
      new SendCommand(10, ''),
      SendQueue.Default,
      ReceiveQueue.Default,
      UseQueue.BypassQueue
    );
    expect(latin1Decode(transport.getWritten())).toBe('10,;');

    // Feed a command that has an empty argument and verify the receiver sees ''
    transport.clearWritten();
    let received: ReceivedCommand | undefined;
    messenger.attach(5, (cmd) => { received = cmd; });
    transport.feedInput(latin1Encode('5,;'));
    await flush();
    expect(received?.readStringArg()).toBe('');

    messenger.dispose();
  });

  test('latin1 characters through pipeline', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    let received: ReceivedCommand | undefined;
    messenger.attach(1, (command) => { received = command; });

    // '\xE9' is 'é' in latin-1; it must not be mangled when passed through the pipeline
    transport.feedInput(latin1Encode('1,\xE9;'));
    await flush();

    expect(received?.readStringArg()).toBe('\xE9');
    messenger.dispose();
  });

  test('collapse command strategy replaces duplicate', async () => {
    // Pause the queue processing by sending a command with reqAc that never
    // gets answered; this gives us time to queue two collapse strategies.
    // Simpler approach: directly verify the collapse via getWritten after flush.

    const written: string[] = [];
    const transport = new LoopbackTransport({
      echoWrites: false,
      onWrite(data) {
        written.push(latin1Decode(data));
      }
    });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    // Queue two commands with the same cmdId using CollapseCommandStrategy.
    // The second enqueue should evict the first, so only the second is sent.
    messenger.queueCommand(new CollapseCommandStrategy(new SendCommand(7, 'first')));
    messenger.queueCommand(new CollapseCommandStrategy(new SendCommand(7, 'second')));

    // Allow the queue worker to drain
    await flush();
    await flush();

    // Only the second command (with arg 'second') should have been written
    expect(written.length).toBeGreaterThan(0);
    const allWritten = written.join('');
    expect(allWritten).not.toContain('first');
    expect(allWritten).toContain('second');

    messenger.dispose();
  });

  test('multiple callbacks for different IDs all fire', async () => {
    const transport = new LoopbackTransport({ echoWrites: false });
    const messenger = new CmdMessenger(transport, BoardType.Bit16);
    await messenger.connect();

    const fired: number[] = [];
    messenger.attach(1, () => fired.push(1));
    messenger.attach(2, () => fired.push(2));
    messenger.attach(3, () => fired.push(3));

    transport.feedInput(latin1Encode('1,a;2,b;3,c;'));
    await flush();

    expect(fired).toContain(1);
    expect(fired).toContain(2);
    expect(fired).toContain(3);
    messenger.dispose();
  });
});
