export interface ProtocolChars {
  fieldSeparator: string;
  commandSeparator: string;
  escapeCharacter: string;
}

export const defaultProtocolChars: ProtocolChars = {
  fieldSeparator: ',',
  commandSeparator: ';',
  escapeCharacter: '/'
};

let activeProtocolChars: ProtocolChars = { ...defaultProtocolChars };

export function getProtocolChars(): ProtocolChars {
  return { ...activeProtocolChars };
}

export function setEscapeChars(
  fieldSeparator: string,
  commandSeparator: string,
  escapeCharacter: string
): void {
  activeProtocolChars = { fieldSeparator, commandSeparator, escapeCharacter };
}

export class IsEscaped {
  private lastChar = '\0';

  constructor(private readonly escapeCharacter = activeProtocolChars.escapeCharacter) {}

  escapedChar(currentChar: string): boolean {
    const escaped = this.lastChar === this.escapeCharacter;
    this.lastChar = currentChar;
    if (this.lastChar === this.escapeCharacter && escaped) {
      this.lastChar = '\0';
    }
    return escaped;
  }
}

export function isEscaped(value: string, index: number, escapeCharacter = activeProtocolChars.escapeCharacter): boolean {
  let count = 0;
  for (let i = index - 1; i >= 0 && value[i] === escapeCharacter; i -= 1) {
    count += 1;
  }
  return count % 2 === 1;
}

export function remove(
  input: string,
  removeChar: string,
  escapeCharacter = activeProtocolChars.escapeCharacter
): string {
  const escaped = new IsEscaped(escapeCharacter);
  let output = '';
  for (const char of input) {
    const currentEscaped = escaped.escapedChar(char);
    if (char !== removeChar || currentEscaped) {
      output += char;
    }
  }
  return output;
}

export function split(
  input: string,
  separator: string,
  escapeCharacter = activeProtocolChars.escapeCharacter,
  removeEmptyEntries = false
): string[] {
  const result: string[] = [];
  let word = '';
  for (let i = 0; i < input.length; i += 1) {
    let char = input[i] ?? '';
    if (char === separator) {
      result.push(word);
      word = '';
    } else {
      if (char === escapeCharacter) {
        word += char;
        if (i < input.length - 1) {
          i += 1;
          char = input[i] ?? '';
        }
      }
      word += char;
    }
  }
  result.push(word);
  return removeEmptyEntries ? result.filter((value) => value !== '') : result;
}

export function escape(input: string, chars = activeProtocolChars): string {
  let output = input;
  for (const char of [chars.escapeCharacter, chars.fieldSeparator, chars.commandSeparator, '\0']) {
    output = output.split(char).join(chars.escapeCharacter + char);
  }
  return output;
}

export function unescape(input: string, escapeCharacter = activeProtocolChars.escapeCharacter): string {
  let output = '';
  for (let i = 0; i < input.length; i += 1) {
    if (input[i] === escapeCharacter) {
      i += 1;
      if (i >= input.length) {
        break;
      }
    }
    output += input[i] ?? '';
  }
  return output;
}
