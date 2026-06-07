import { appendFileSync } from 'node:fs';

export class Logger {
  private static _isEnabled = false;
  private static _logFileName = '';
  private static _directFlush = false;

  static get isEnabled(): boolean { return Logger._isEnabled; }
  static set isEnabled(value: boolean) { Logger._isEnabled = value; }

  static get isOpen(): boolean { return Logger._logFileName !== ''; }

  static get logFileName(): string { return Logger._logFileName; }

  static get directFlush(): boolean { return Logger._directFlush; }
  static set directFlush(value: boolean) { Logger._directFlush = value; }

  static open(logFileName?: string): boolean {
    Logger._logFileName = logFileName ?? 'cmdmessenger.log';
    Logger._isEnabled = true;
    return true;
  }

  static close(): void {
    Logger._logFileName = '';
    Logger._isEnabled = false;
  }

  static log(message: string): void {
    if (!Logger._isEnabled || !Logger._logFileName) return;
    appendFileSync(Logger._logFileName, message, 'utf-8');
  }

  static logLine(message: string): void {
    Logger.log(message + '\n');
  }
}
