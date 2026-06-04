import { describe, expect, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { BoardType } from '../src/enums.js';
import { LoopbackTransport } from '../src/transport/loopbackTransport.js';
import { LoopbackFirmware } from './loopbackFirmware.js';
import { loopbackScenarioCases, waitForBootAck } from './loopbackScenarios.js';

async function createMessenger(): Promise<{
  messenger: CmdMessenger;
  transport: LoopbackTransport;
}> {
  const firmware = new LoopbackFirmware();
  const transport = new LoopbackTransport({
    echoWrites: false,
    onWrite(data, loopback) {
      firmware.handleWrite(data, loopback);
    }
  });
  const messenger = new CmdMessenger(transport, BoardType.Bit16);
  await messenger.connect();

  const boot = waitForBootAck(messenger);
  firmware.sendBootAck(transport);
  await boot;

  return { messenger, transport };
}

describe('loopback scenarios', () => {
  test.each(loopbackScenarioCases)('$name', async ({ run }) => {
    const { messenger, transport } = await createMessenger();

    try {
      await run(messenger);
    } finally {
      await messenger.disconnect();
      messenger.dispose();
      transport.dispose();
    }

    expect(transport.isConnected()).toBe(false);
  });
});
