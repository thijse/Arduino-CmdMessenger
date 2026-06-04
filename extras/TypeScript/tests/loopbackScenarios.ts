import { expect } from 'vitest';
import { CmdMessenger } from '../src/cmdMessenger.js';
import { escape, unescape } from '../src/escaping.js';
import { ReceivedCommand } from '../src/receivedCommand.js';
import { SendCommand } from '../src/sendCommand.js';
import { LoopbackCommand } from './loopbackCommands.js';

export const ACK_TIMEOUT_MS = 2000;
export const BOOT_TIMEOUT_MS = 5000;

export interface LoopbackScenarioOptions {
  ackTimeoutMs?: number;
}

export type LoopbackScenario = {
  name: string;
  run: (messenger: CmdMessenger, options?: LoopbackScenarioOptions) => Promise<void>;
};

const floatCases: Array<readonly [number, number]> = [
  [0, 0],
  [1, 2],
  [-5, 10],
  [3.14, 2.71],
  [1000.5, -500.25]
];

function timeout(options?: LoopbackScenarioOptions): number {
  return options?.ackTimeoutMs ?? ACK_TIMEOUT_MS;
}

export function waitForCommand(
  messenger: CmdMessenger,
  cmdId: number,
  timeoutMs: number
): Promise<ReceivedCommand> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      reject(new Error(`Timed out waiting for command ${cmdId} after ${timeoutMs} ms`));
    }, timeoutMs);

    messenger.attach(cmdId, (command) => {
      clearTimeout(timer);
      resolve(command);
    });
  });
}

export async function waitForBootAck(
  messenger: CmdMessenger,
  timeoutMs = BOOT_TIMEOUT_MS
): Promise<ReceivedCommand> {
  const boot = await waitForCommand(messenger, LoopbackCommand.Acknowledge, timeoutMs);
  expect(boot.cmdId).toBe(LoopbackCommand.Acknowledge);
  return boot;
}

export async function assertPingReturnsPong(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  const reply = await messenger.sendCommand(
    new SendCommand(LoopbackCommand.Ping).withAck(LoopbackCommand.Pong, timeout(options))
  );

  expect(reply.ok).toBe(true);
  expect(reply.cmdId).toBe(LoopbackCommand.Pong);
  expect(reply.readStringArg()).toBe('pong');
}

export async function assertEchoStringRoundTrips(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const text of ['hello', 'Hello, World!', '  spaced  ', 'special chars: !@#$%^&*()']) {
    await assertEchoString(messenger, text, options);
  }
}

export async function assertEchoStringWithSpecialCharsRoundTrips(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const text of ['contains, comma', 'contains; semicolon', 'contains/ slash', 'a, b; c/ d']) {
    await assertEchoString(messenger, text, options);
  }
}

export async function assertAddFloatsReturnsSumAndDifference(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const [first, second] of floatCases) {
    const command = new SendCommand(LoopbackCommand.AddFloats)
      .addArgument(first)
      .addArgument(second)
      .withAck(LoopbackCommand.AddFloatsResult, timeout(options));
    const reply = await messenger.sendCommand(command);

    expect(reply.ok).toBe(true);
    expect(reply.readFloatArg()).toBeCloseTo(first + second, 4);
    expect(reply.readFloatArg()).toBeCloseTo(first - second, 4);
  }
}

export async function assertEchoInt32RoundTrips(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const value of [0, 42, -1, 2 ** 31 - 1, -(2 ** 31)]) {
    const reply = await messenger.sendCommand(
      new SendCommand(LoopbackCommand.EchoInt, value).withAck(LoopbackCommand.EchoIntResult, timeout(options))
    );

    expect(reply.ok).toBe(true);
    expect(reply.readInt32Arg()).toBe(value);
  }
}

export async function assertEchoInt16RoundTrips(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const value of [0, 1234, -1234, 32767, -32768]) {
    const reply = await messenger.sendCommand(
      new SendCommand(LoopbackCommand.EchoInt16, value).withAck(LoopbackCommand.EchoInt16Result, timeout(options))
    );

    expect(reply.ok).toBe(true);
    expect(reply.readInt16Arg()).toBe(value);
  }
}

export async function assertEchoBoolRoundTrips(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const value of [true, false]) {
    const command = new SendCommand(LoopbackCommand.EchoBool)
      .addArgument(value)
      .withAck(LoopbackCommand.EchoBoolResult, timeout(options));
    const reply = await messenger.sendCommand(command);

    expect(reply.ok).toBe(true);
    expect(reply.readInt32Arg()).toBe(value ? 1 : 0);
  }
}

export async function assertEchoDoubleRoundTripsAsFloatText(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const value of [0, Math.PI, -1.5e-10, 1.5e10]) {
    const command = new SendCommand(LoopbackCommand.EchoDouble)
      .addArgument(value)
      .withAck(LoopbackCommand.EchoDoubleResult, timeout(options));
    const reply = await messenger.sendCommand(command);

    expect(reply.ok).toBe(true);
    const expected = float32Value(value);
    const actual = reply.readFloatArg();
    const tolerance = Math.max(1e-6, Math.abs(expected) * 1e-6);
    expect(actual).toBeGreaterThanOrEqual(expected - tolerance);
    expect(actual).toBeLessThanOrEqual(expected + tolerance);
  }
}

export async function assertMultiArgsAllTypesRoundTrip(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  const command = new SendCommand(LoopbackCommand.MultiArgs)
    .addArgument(1234)
    .addArgument(3.14)
    .addArgument(escape('mixed, args; here'))
    .addArgument(true)
    .withAck(LoopbackCommand.MultiArgsResult, timeout(options));

  const reply = await messenger.sendCommand(command);

  expect(reply.ok).toBe(true);
  expect(reply.readInt16Arg()).toBe(1234);
  expect(reply.readFloatArg()).toBeCloseTo(3.14, 4);
  expect(unescape(reply.readStringArg())).toBe('mixed, args; here');
  expect(reply.readInt32Arg()).toBe(1);
}

export async function assertUnknownCommandTriggersError(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  const error = waitForCommand(messenger, LoopbackCommand.Error, timeout(options));
  await messenger.sendCommand(new SendCommand(99));
  const reply = await error;

  expect(reply.cmdId).toBe(LoopbackCommand.Error);
}

export async function assertRepeatedCommandsAllRoundTrip(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (let index = 0; index < 20; index += 1) {
    const reply = await messenger.sendCommand(
      new SendCommand(LoopbackCommand.EchoInt, index).withAck(LoopbackCommand.EchoIntResult, timeout(options))
    );

    expect(reply.ok).toBe(true);
    expect(reply.readInt32Arg()).toBe(index);
  }
}

export const loopbackScenarioCases: LoopbackScenario[] = [
  { name: 'ping returns pong', run: assertPingReturnsPong },
  { name: 'echo string round trips', run: assertEchoStringRoundTrips },
  { name: 'escaped string round trips', run: assertEchoStringWithSpecialCharsRoundTrips },
  { name: 'add floats returns sum and difference', run: assertAddFloatsReturnsSumAndDifference },
  { name: 'echo int32 round trips', run: assertEchoInt32RoundTrips },
  { name: 'echo int16 round trips', run: assertEchoInt16RoundTrips },
  { name: 'echo bool round trips', run: assertEchoBoolRoundTrips },
  { name: 'echo double round trips as float text', run: assertEchoDoubleRoundTripsAsFloatText },
  { name: 'multi args all types round trip', run: assertMultiArgsAllTypesRoundTrip },
  { name: 'unknown command triggers error', run: assertUnknownCommandTriggersError },
  { name: 'repeated commands all round trip', run: assertRepeatedCommandsAllRoundTrip }
];

export async function assertAllLoopbackScenarios(
  messenger: CmdMessenger,
  options?: LoopbackScenarioOptions
): Promise<void> {
  for (const scenario of loopbackScenarioCases) {
    await scenario.run(messenger, options);
  }
}

async function assertEchoString(
  messenger: CmdMessenger,
  text: string,
  options?: LoopbackScenarioOptions
): Promise<void> {
  const reply = await messenger.sendCommand(
    new SendCommand(LoopbackCommand.Echo, escape(text)).withAck(LoopbackCommand.EchoResult, timeout(options))
  );

  expect(reply.ok).toBe(true);
  expect(unescape(reply.readStringArg())).toBe(text);
}

function float32Value(value: number): number {
  const buffer = new ArrayBuffer(4);
  const view = new DataView(buffer);
  view.setFloat32(0, value, true);
  return view.getFloat32(0, true);
}
