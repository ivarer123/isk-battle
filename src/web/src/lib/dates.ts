const dayMonth = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short', timeZone: 'UTC' });
const monthYear = new Intl.DateTimeFormat('sv-SE', { month: 'long', year: 'numeric', timeZone: 'UTC' });

/** API dates are ISO "yyyy-mm-dd" Stockholm dates; treat them as UTC calendar dates to avoid shifts. */
export function parseDate(iso: string): Date {
  return new Date(`${iso}T00:00:00Z`);
}

export function toIso(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function addDays(iso: string, days: number): string {
  const date = parseDate(iso);
  date.setUTCDate(date.getUTCDate() + days);
  return toIso(date);
}

export function formatDayMonth(iso: string): string {
  return dayMonth.format(parseDate(iso));
}

export function formatMonthYear(iso: string): string {
  const text = monthYear.format(parseDate(iso));
  return text.charAt(0).toUpperCase() + text.slice(1);
}
