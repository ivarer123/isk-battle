const percent = new Intl.NumberFormat('sv-SE', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  signDisplay: 'exceptZero',
});

const sek = new Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK', maximumFractionDigits: 0 });

/** "+12,40 %" / "−3,15 %" with Swedish separators. */
export function formatPercent(value: number): string {
  return `${percent.format(value)} %`;
}

export function formatOre(ore: number): string {
  return sek.format(ore / 100);
}

/**
 * Parses user input such as "12,40", "-3.15", "+5", "−1,5 %" or "1 000". Returns null if it isn't a number.
 */
export function parsePercent(input: string): number | null {
  const normalized = input
    .trim()
    .replace(/%$/, '')
    .replace(/[\s  ]/g, '')
    .replace(/^[−–]/, '-')
    .replace(',', '.');
  if (!/^[+-]?(\d+(\.\d*)?|\.\d+)$/.test(normalized)) return null;
  const value = Number(normalized);
  return Number.isFinite(value) ? value : null;
}

export function decimals(value: number): number {
  const text = String(value);
  const dot = text.indexOf('.');
  return dot < 0 ? 0 : text.length - dot - 1;
}

export const MIN_YTD = -99.99;
export const MAX_YTD = 1000;
