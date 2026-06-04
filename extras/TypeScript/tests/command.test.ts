import { describe, expect, test } from 'vitest';
import { BoardType } from '../src/enums.js';
import { escape, unescape } from '../src/escaping.js';
import { ReceivedCommand } from '../src/receivedCommand.js';
import { SendCommand } from '../src/sendCommand.js';

const context = {
  fieldSeparator: ',',
  commandSeparator: ';',
  escapeCharacter: '/',
  boardType: BoardType.Bit16
};

describe('commands', () => {
  test('send command stores text args without auto-escaping', () => {
    const command = new SendCommand(3, 'a,b', true);
    command.communicationManager = context;
    command.initArguments();

    expect(command.arguments).toEqual(['a,b', '1']);
    expect(command.commandString()).toBe('3,a,b,1;');
  });

  test('send command options configure ack and args', () => {
    const command = new SendCommand(5, { args: ['hello'], ackCmdId: 6, timeout: 1000 });
    command.communicationManager = context;
    command.initArguments();

    expect(command.reqAc).toBe(true);
    expect(command.ackCmdId).toBe(6);
    expect(command.timeout).toBe(1000);
    expect(command.arguments).toEqual(['hello']);
  });

  test('withAck supports args plus ack fluently', () => {
    const command = new SendCommand(5, 'hello').withAck(6, 1000);

    expect(command.reqAc).toBe(true);
    expect(command.ackCmdId).toBe(6);
    expect(command.timeout).toBe(1000);
  });

  test('explicit binary widths round-trip through received readers', () => {
    const command = new SendCommand(1)
      .addBinInt16Argument(-1234)
      .addBinUint32Argument(4_000_000_000)
      .addBinFloatArgument(3.5);
    command.communicationManager = context;
    command.initArguments();

    const received = new ReceivedCommand(['1', ...command.arguments]);
    received.communicationManager = context;

    expect(received.readBinInt16Arg()).toBe(-1234);
    expect(received.readBinUint32Arg()).toBe(4_000_000_000);
    expect(received.readBinFloatArg()).toBeCloseTo(3.5);
  });

  test('received command typed readers advance cursor', () => {
    const received = new ReceivedCommand(['4', '123', '3.14', escape('a,b'), '1']);
    received.communicationManager = context;

    expect(received.cmdId).toBe(4);
    expect(received.readInt32Arg()).toBe(123);
    expect(received.readFloatArg()).toBeCloseTo(3.14);
    expect(unescape(received.readStringArg())).toBe('a,b');
    expect(received.readBoolArg()).toBe(true);
  });

  test('format reader returns values in order', () => {
    const received = new ReceivedCommand(['4', '123', '3.14', 'label']);
    received.communicationManager = context;

    expect(received.read('ifs')).toEqual([123, 3.14, 'label']);
  });
});
