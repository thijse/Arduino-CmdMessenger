/** Encode a JavaScript string as ISO-8859-1 bytes. */
export function latin1Encode(value: string): Uint8Array {
  const bytes = new Uint8Array(value.length);
  for (let i = 0; i < value.length; i += 1) {
    const code = value.charCodeAt(i);
    if (code > 0xff) {
      throw new RangeError(`Character U+${code.toString(16).padStart(4, '0')} is outside ISO-8859-1.`);
    }
    bytes[i] = code;
  }
  return bytes;
}

/** Decode ISO-8859-1 bytes into a JavaScript string, preserving 0..255. */
export function latin1Decode(bytes: Uint8Array): string {
  const chunkSize = 8192;
  const chunks: string[] = [];
  for (let i = 0; i < bytes.length; i += chunkSize) {
    const end = Math.min(i + chunkSize, bytes.length);
    let chunk = '';
    for (let j = i; j < end; j += 1) {
      chunk += String.fromCharCode(bytes[j] ?? 0);
    }
    chunks.push(chunk);
  }
  return chunks.join('');
}

export function concatBytes(chunks: readonly Uint8Array[]): Uint8Array {
  const total = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const out = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    out.set(chunk, offset);
    offset += chunk.length;
  }
  return out;
}
