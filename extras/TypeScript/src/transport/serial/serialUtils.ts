import type { PortInfo, SerialPort } from 'serialport';

export const commonBaudRates = [115200, 57600, 9600] as const;

type SerialPortConstructor = typeof SerialPort;

async function loadSerialPort(): Promise<SerialPortConstructor | null> {
  try {
    const module = await import('serialport');
    return module.SerialPort;
  } catch {
    return null;
  }
}

export async function getPortInfos(): Promise<PortInfo[]> {
  const serialPort = await loadSerialPort();
  if (serialPort === null) {
    return [];
  }
  return serialPort.list();
}

export async function getPortNames(): Promise<string[]> {
  const ports = await getPortInfos();
  return ports.map((port) => port.path);
}

export async function portExists(serialPortName: string): Promise<boolean> {
  return (await getPortNames()).includes(serialPortName);
}

export function getSupportedBaudRates(_serialPortName?: string): number[] {
  return [...commonBaudRates];
}
