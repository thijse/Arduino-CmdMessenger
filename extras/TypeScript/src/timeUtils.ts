const start = globalThis.performance?.timeOrigin ?? Date.now();

/** Milliseconds since process start-ish, matching Arduino/C# style use. */
export function millis(): number {
  if (globalThis.performance !== undefined) {
    return Math.floor(globalThis.performance.now());
  }
  return Date.now() - start;
}

export function seconds(): number {
  return Math.floor(millis() / 1000);
}

export function hasExpired(startMs: number, timeoutMs: number): boolean {
  return timeoutMs >= 0 && millis() - startMs >= timeoutMs;
}

export function delay(timeoutMs: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, timeoutMs);
  });
}
