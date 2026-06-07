import { afterEach, beforeEach, describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { latin1Decode, latin1Encode } from '../src/encoding.js';
import { BoardType, ReceiveQueue, SendQueue, UseQueue } from '../src/enums.js';
import { IsEscaped, split } from '../src/escaping.js';
import { ReceivedCommand } from '../src/receivedCommand.js';
import { SendCommand } from '../src/sendCommand.js';
import { LoopbackTransport } from '../src/transport/loopbackTransport.js';

// Command IDs used exclusively in these tests
const REQUEST = 52;
const RESPONSE = 53;

function flush(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

/**
 * Creates a CmdMessenger with a LoopbackTransport whose onWrite callback
 * parses the written command, extracts the raw (escaped) argument field, and
 * feeds it back as the first argument of a RESPONSE command.
 *
 * Binary arguments are already escape-encoded on the wire, so echoing the raw
 * field verbatim is sufficient for round-trip verification.
 */
function makeMessenger(): { messenger: CmdMessenger } {
  const transport = new LoopbackTransport({
    echoWrites: false,
    onWrite(data, loopback) {
      const text = latin1Decode(data);
      // Find the command terminator (';') while honouring escape sequences.
      // A naïve indexOf would be fooled by an escaped ';' inside binary payload.
      const escaped = new IsEscaped('/');
      let semi = -1;
      for (let i = 0; i < text.length; i += 1) {
        const currentEscaped = escaped.escapedChar(text[i] ?? '');
        if (text[i] === ';' && !currentEscaped) {
          semi = i;
          break;
        }
      }
      if (semi < 0) return;
      const body = text.slice(0, semi);
      // Split on unescaped field separators (','), escape char is '/'
      const parts = split(body, ',', '/');
      // parts[0] = cmdId, parts[1] = raw binary arg field
      const rawArg = parts[1] ?? '';
      // Echo raw arg back as RESPONSE command
      loopback.feedInput(latin1Encode(`${RESPONSE},${rawArg};`));
    }
  });
  const messenger = new CmdMessenger(transport, BoardType.Bit16);
  void messenger.connect();
  return { messenger };
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

/**
 * Returns a float32 value constructed from the given four bytes (little-endian).
 */
function floatFromBytes(b0: number, b1: number, b2: number, b3: number): number {
  const buf = new ArrayBuffer(4);
  const view = new DataView(buf);
  view.setUint8(0, b0);
  view.setUint8(1, b1);
  view.setUint8(2, b2);
  view.setUint8(3, b3);
  return view.getFloat32(0, true);
}

/**
 * Rounds a JS number to the nearest float32 value.
 */
function toFloat32(value: number): number {
  const buf = new ArrayBuffer(4);
  new DataView(buf).setFloat32(0, value, true);
  return new DataView(buf).getFloat32(0, true);
}

describe('binaryData round-trips through CmdMessenger pipeline', () => {
  let messenger: CmdMessenger;

  beforeEach(() => {
    ({ messenger } = makeMessenger());
  });

  afterEach(() => {
    messenger.dispose();
  });

  // ---- bool ----

  test('binary bool true round-trips', async () => {
    const send = new SendCommand(REQUEST).addBinBoolArgument(true);
    const response = await roundTrip(messenger, send);
    expect(response.readBinBoolArg()).toBe(true);
  });

  test('binary bool false round-trips', async () => {
    const send = new SendCommand(REQUEST).addBinBoolArgument(false);
    const response = await roundTrip(messenger, send);
    expect(response.readBinBoolArg()).toBe(false);
  });

  // ---- Int16 ----

  test.each([[-32768], [-1], [0], [1], [32767]])(
    'binary Int16 %d round-trips',
    async (v) => {
      const send = new SendCommand(REQUEST).addBinInt16Argument(v);
      const response = await roundTrip(messenger, send);
      expect(response.readBinInt16Arg()).toBe(v);
    }
  );

  // ---- Int32 ----

  test.each([[-2147483648], [-1], [0], [1], [2147483647]])(
    'binary Int32 %d round-trips',
    async (v) => {
      const send = new SendCommand(REQUEST).addBinInt32Argument(v);
      const response = await roundTrip(messenger, send);
      expect(response.readBinInt32Arg()).toBe(v);
    }
  );

  // ---- Float ----

  test.each([[0.5], [-1.25], [3.14]])(
    'binary float %f round-trips',
    async (v) => {
      const send = new SendCommand(REQUEST).addBinFloatArgument(v);
      const response = await roundTrip(messenger, send);
      expect(response.readBinFloatArg()).toBeCloseTo(v, 5);
    }
  );

  test('binary float whose bytes contain protocol special characters round-trips', async () => {
    // bytes: 0x2C (',') 0x3B (';') 0x2F ('/') 0x00 ('\0') — all four protocol-sensitive bytes
    const specialFloat = floatFromBytes(0x2c, 0x3b, 0x2f, 0x00);
    const expected = toFloat32(specialFloat);

    const send = new SendCommand(REQUEST).addBinFloatArgument(specialFloat);
    const response = await roundTrip(messenger, send);
    expect(response.readBinFloatArg()).toBe(expected);
  });

  // ---- Double (Bit16 board: encoded as float32) ----

  test.each([[0.0], [1.0], [-1.5], [3.14]])(
    'binary double %f round-trips as float32 on Bit16 board',
    async (v) => {
      const expected = toFloat32(v);
      const send = new SendCommand(REQUEST).addBinDoubleArgument(v);
      const response = await roundTrip(messenger, send);
      const actual = response.readBinDoubleArg();
      expect(actual).toBeCloseTo(expected, 5);
    }
  );
});
