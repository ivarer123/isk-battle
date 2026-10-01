import sv from './sv.json';

export type MessageKey = keyof typeof sv;

const messages: Record<string, string> = sv;

/** Looks up a Swedish string and fills `{placeholders}`. English can be added later as another table. */
export function t(key: MessageKey, values?: Record<string, string | number>): string {
  const template = messages[key] ?? key;
  return values ? template.replace(/\{(\w+)\}/g, (match, name: string) => String(values[name] ?? match)) : template;
}

/** For server-provided keys (enum values, problem codes) that may not exist. */
export function tryT(key: string, fallback: string): string {
  return messages[key] ?? fallback;
}
