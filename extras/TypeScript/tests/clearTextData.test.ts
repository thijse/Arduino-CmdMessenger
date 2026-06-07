import { afterEach, beforeEach, describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { BoardType, ReceiveQueue, SendQueue, UseQueue } from '../src/enums.js';
import { escape, unescape } from '../src/escaping.js';
import { ReceivedCommand } from '../src/receivedCommand.js';
import { SendCommand } from '../src/sendCommand.js';
import { LoopbackTransport } from '../src/transport/loopbackTransport.js';

// Command IDs used exclusively in these tests
const REQUEST = 50;
const RESPONSE = 51;

function flush(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

/**
 * Creates a CmdMessenger with a LoopbackTransport whose onWrite callback
 * echoes the first argument of the incoming request back as a Response command.
 */
function makeMessenger(): { messenger: CmdMessenger; transport: LoopbackTransport } {
  const transport = new LoopbackTransport({
    echoWrites: false,
    onWrite(data, loopback) {
      const text = latin1Decode(data);
      // Parse: "<cmdId>,<arg>;" — pull out raw arg (may be escaped)
      const semi = text.indexOf(';');
      if (semi < 0) return;
      const body = text.slice(0, semi);
      const commaIdx = body.indexOf(',');
      const rawArg = commaIdx >= 0 ? body.slice(commaIdx + 1) : '';
      // Echo it straight back as Response
      loopback.feedInput(latin1Encode(`${RESPONSE},${rawArg};`));
    }
  });
  const messenger = new CmdMessenger(transport, BoardType.Bit16);
  void messenger.connect();
  return { messenger, transport };
}

/**
 * Sends a request command and waits for the echoed response, returning it.
 */
async function roundTrip(messenger: CmdMessenger, send: SendCommand): Promise<ReceivedCommand> {
  const responsePromise = new Promise<ReceivedCommand>((resolve) => {
    messenger.attach(RESPONSE, (cmd) => resolve(cmd));
  });
  await messenger.sendCommand(send, SendQueue.Default, ReceiveQueue.Default, UseQueue.BypassQueue);
  await flush();
  return responsePromise;
}

describe('clearTextData round-trips through CmdMessenger pipeline', () => {
  let messenger: CmdMessenger;

  beforeEach(() => {
    ({ messenger } = makeMessenger());
  });

  afterEach(() => {
    messenger.dispose();
  });

  test('bool true round-trips', async () => {
    const send = new SendCommand(REQUEST).addArgument(true);
    const response = await roundTrip(messenger, send);
    expect(response.readBoolArg()).toBe(true);
  });

  test('bool false round-trips', async () => {
    const send = new SendCommand(REQUEST).addArgument(false);
    const response = await roundTrip(messenger, send);
    expect(response.readBoolArg()).toBe(false);
  });

  test.each([[-32768], [-1], [0], [1], [32767]])(
    'Int16 %d round-trips',
    async (v) => {
      const send = new SendCommand(REQUEST).addArgument(v);
      const response = await roundTrip(messenger, send);
      expect(response.readInt16Arg()).toBe(v);
    }
  );

  test.each([[-2147483648], [-1], [0], [1], [2147483647]])(
    'Int32 %d round-trips',
    async (v) => {
      const send = new SendCommand(REQUEST).addArgument(v);
      const response = await roundTrip(messenger, send);
      expect(response.readInt32Arg()).toBe(v);
    }
  );

  test.each([[0.5], [-1.25], [3.14]])(
    'float %f round-trips',
    async (v) => {
      const send = new SendCommand(REQUEST).addArgument(v);
      const response = await roundTrip(messenger, send);
      expect(response.readFloatArg()).toBeCloseTo(v, 5);
    }
  );

  test('float NaN round-trips', async () => {
    // NaN cannot be represented as a text numeric argument, so send as a string token
    const send = new SendCommand(REQUEST, 'NaN');
    const response = await roundTrip(messenger, send);
    expect(Number.isNaN(Number(response.readStringArg()))).toBe(true);
  });

  test('float Infinity round-trips', async () => {
    const send = new SendCommand(REQUEST, 'Infinity');
    const response = await roundTrip(messenger, send);
    expect(Number(response.readStringArg())).toBe(Infinity);
  });

  test('string round-trips', async () => {
    const text = 'hello world';
    const send = new SendCommand(REQUEST, escape(text));
    const response = await roundTrip(messenger, send);
    expect(unescape(response.readStringArg())).toBe(text);
  });
});
