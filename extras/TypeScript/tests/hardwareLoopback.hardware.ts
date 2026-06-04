import { describe, test } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { BoardType } from '../src/enums.js';
import { unescape } from '../src/escaping.js';
import { SendCommand } from '../src/sendCommand.js';
import { delay } from '../src/timeUtils.js';
import { SerialTransport } from '../src/transport/serial/serialTransport.js';
import { getPortInfos } from '../src/transport/serial/serialUtils.js';
import { LoopbackCommand } from './loopbackCommands.js';
import {
  assertAllLoopbackScenarios,
  assertPingReturnsPong,
  waitForBootAck
} from './loopbackScenarios.js';

interface HardwareTarget {
  portName: string;
  baudRate: number;
  model: string;
  id: string;
  discovered: boolean;
}

type SerialPortInfo = Awaited<ReturnType<typeof getPortInfos>>[number];

const HARDWARE_ACK_TIMEOUT_MS = 3000;
const HARDWARE_BOOT_TIMEOUT_MS = 8000;

describe.sequential('hardware loopback', () => {
  test('runs shared scenarios against connected boards', async () => {
    const targets = await resolveHardwareTargets();

    for (const target of targets) {
      await runOnHardwareTarget(target);
    }
  }, 300000);
});

async function runOnHardwareTarget(target: HardwareTarget): Promise<void> {
  const transport = new SerialTransport({
    portName: target.portName,
    baudRate: target.baudRate,
    dtrEnable: true,
    rtsEnable: true,
    timeout: HARDWARE_ACK_TIMEOUT_MS
  });
  const messenger = new CmdMessenger(transport, BoardType.Bit16);
  messenger.printLfCr = true;

  const bootAck = waitForBootAck(messenger, HARDWARE_BOOT_TIMEOUT_MS).then(
    () => true,
    () => false
  );
  const connected = await messenger.connect();
  if (!connected) {
    throw new Error(`Failed to open ${target.portName} at ${target.baudRate}`);
  }

  try {
    if (!(await bootAck)) {
      await assertPingReturnsPong(messenger, { ackTimeoutMs: HARDWARE_ACK_TIMEOUT_MS });
    }
    await assertAllLoopbackScenarios(messenger, { ackTimeoutMs: HARDWARE_ACK_TIMEOUT_MS });
  } finally {
    await messenger.disconnect();
    messenger.dispose();
    transport.dispose();
  }
}

async function resolveHardwareTargets(): Promise<HardwareTarget[]> {
  const baudRate = hardwareBaudRate();
  const envPort = firstEnvironmentValue('CMDMSG_HW_PORT', 'CMDMESSENGER_PORT');
  const envModel = firstEnvironmentValue('CMDMSG_HW_BOARD', 'CMDMESSENGER_BOARD');

  if (envPort !== undefined) {
    return [
      {
        portName: envPort,
        baudRate,
        model: envModel ?? envPort,
        id: '',
        discovered: false
      }
    ];
  }

  const allPorts = await getPortInfos();
  const ports = allPorts.filter(isUsbSerialPort);
  if (ports.length === 0) {
    throw new Error('No USB serial ports found. Connect a board or set CMDMSG_HW_PORT.');
  }

  const discovered: HardwareTarget[] = [];
  for (const port of ports) {
    const target = await queryIdentity(port.path, baudRate);
    if (target !== null && target.model !== 'UNPROVISIONED') {
      discovered.push(target);
    }
  }

  const filtered = discovered.filter((target) => targetMatches(target, envModel));
  if (filtered.length > 0) {
    return filtered;
  }

  if (envModel !== undefined) {
    throw new Error(`No provisioned board matching ${envModel} was found.`);
  }

  if (ports.length === 1) {
    const portName = ports[0]?.path;
    if (portName !== undefined) {
      return [
        {
          portName,
          baudRate,
          model: 'UNIDENTIFIED',
          id: '',
          discovered: false
        }
      ];
    }
  }

  throw new Error('No provisioned loopback boards discovered. Set CMDMSG_HW_PORT to test a specific port.');
}

async function queryIdentity(portName: string, baudRate: number): Promise<HardwareTarget | null> {
  const transport = new SerialTransport({
    portName,
    baudRate,
    dtrEnable: true,
    rtsEnable: true,
    timeout: HARDWARE_ACK_TIMEOUT_MS
  });
  const messenger = new CmdMessenger(transport, BoardType.Bit16);
  messenger.printLfCr = true;

  try {
    if (!(await messenger.connect())) {
      return null;
    }

    await delay(2500);
    const reply = await messenger.sendCommand(
      new SendCommand(LoopbackCommand.WhoAmI).withAck(LoopbackCommand.WhoAmIResult, HARDWARE_ACK_TIMEOUT_MS)
    );

    if (!reply.ok) {
      return null;
    }

    return {
      portName,
      baudRate,
      model: unescape(reply.readStringArg()),
      id: unescape(reply.readStringArg()),
      discovered: true
    };
  } catch {
    return null;
  } finally {
    await messenger.disconnect();
    messenger.dispose();
    transport.dispose();
    await delay(200);
  }
}

function hardwareBaudRate(): number {
  const value = firstEnvironmentValue('CMDMSG_HW_BAUD', 'CMDMESSENGER_BAUD');
  const parsed = value === undefined ? 115200 : Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error(`Invalid hardware baud rate: ${value}`);
  }
  return parsed;
}

function firstEnvironmentValue(...names: string[]): string | undefined {
  for (const name of names) {
    const value = process.env[name];
    if (value !== undefined && value.trim() !== '') {
      return value.trim();
    }
  }
  return undefined;
}

function targetMatches(target: HardwareTarget, requested: string | undefined): boolean {
  if (requested === undefined || requested.toLowerCase() === 'all') {
    return true;
  }
  return (
    target.model.toLowerCase() === requested.toLowerCase() ||
    target.portName.toLowerCase() === requested.toLowerCase()
  );
}

function isUsbSerialPort(port: SerialPortInfo): boolean {
  const pnpId = port.pnpId?.toUpperCase() ?? '';
  return port.vendorId !== undefined || pnpId.startsWith('USB\\');
}
