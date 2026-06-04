import { describe, expect, test } from 'vitest';
import { getSupportedBaudRates } from '../src/transport/serial/serialUtils.js';
import { SerialSettings } from '../src/transport/serial/serialSettings.js';

describe('SerialSettings', () => {
  test('uses C#/Python-compatible defaults', () => {
    const settings = new SerialSettings();

    expect(settings.portName).toBe('');
    expect(settings.baudRate).toBe(9600);
    expect(settings.parity).toBe('none');
    expect(settings.dataBits).toBe(8);
    expect(settings.stopBits).toBe(1);
    expect(settings.dtrEnable).toBe(false);
    expect(settings.rtsEnable).toBe(false);
    expect(settings.timeout).toBe(500);
    expect(settings.isValid()).toBe(false);
  });

  test('validates configured serial settings', () => {
    const settings = new SerialSettings({ portName: 'COM3', baudRate: 115200 });

    expect(settings.isValid()).toBe(true);
  });
});

describe('SerialUtils', () => {
  test('exposes common baud rates without requiring serialport at call site', () => {
    expect(getSupportedBaudRates()).toEqual([115200, 57600, 9600]);
  });
});
