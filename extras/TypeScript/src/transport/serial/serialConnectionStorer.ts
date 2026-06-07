import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname } from 'node:path';
import { homedir } from 'node:os';
import { join } from 'node:path';

export interface SerialConnectionManagerSettings {
  port: string;
  baudRate: number;
}

export interface ISerialConnectionStorer {
  storeSettings(settings: SerialConnectionManagerSettings): void;
  retrieveSettings(): SerialConnectionManagerSettings | null;
}

export class JsonSerialConnectionStorer implements ISerialConnectionStorer {
  private readonly _path: string;

  constructor(path?: string) {
    this._path = path ?? join(homedir(), '.cmdmessenger', 'serial-connection.json');
  }

  storeSettings(settings: SerialConnectionManagerSettings): void {
    const dir = dirname(this._path);
    if (!existsSync(dir)) {
      mkdirSync(dir, { recursive: true });
    }
    writeFileSync(this._path, JSON.stringify(settings, null, 2), 'utf-8');
  }

  retrieveSettings(): SerialConnectionManagerSettings | null {
    if (!existsSync(this._path)) return null;
    try {
      const data = JSON.parse(readFileSync(this._path, 'utf-8'));
      if (typeof data.port === 'string' && typeof data.baudRate === 'number') {
        return data as SerialConnectionManagerSettings;
      }
      return null;
    } catch {
      return null;
    }
  }
}
