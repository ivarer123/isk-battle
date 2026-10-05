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

const weekdayDayMonth = new Intl.DateTimeFormat('sv-SE', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' });
const stockholmParts = new Intl.DateTimeFormat('en-GB', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: 'Europe/Stockholm',
});

/** "måndag 21 september" for an ISO date. */
export function formatWeekdayDayMonth(iso: string): string {
  return weekdayDayMonth.format(parseDate(iso));
}

/** The Stockholm calendar date ("yyyy-mm-dd") and wall-clock time of an instant. */
function stockholm(instant: Date): { date: string; hour: number; minute: number } {
  const parts = Object.fromEntries(stockholmParts.formatToParts(instant).map((p) => [p.type, p.value]));
  return { date: `${parts.year}-${parts.month}-${parts.day}`, hour: Number(parts.hour), minute: Number(parts.minute) };
}

/**
 * A Stockholm time relative to `today` (the server's Stockholm date): "ikväll kl. 22.30", "idag kl. 09.00",
 * "imorgon kl. 09.00" or "fredag 9 oktober kl. 22.30". A window closing at midnight is shown as 23.59 the day
 * before, matching how the rules are written ("Sunday 23:59").
 */
export function formatStockholmDateTime(instant: string, today: string): string {
  let at = new Date(instant);
  let { date, hour, minute } = stockholm(at);
  if (hour === 0 && minute === 0) {
    at = new Date(at.getTime() - 60_000);
    ({ date, hour, minute } = stockholm(at));
  }
  const time = `kl. ${String(hour).padStart(2, '0')}.${String(minute).padStart(2, '0')}`;
  if (date === today) return `${hour >= 18 ? 'ikväll' : 'idag'} ${time}`;
  if (date === addDays(today, 1)) return `imorgon ${time}`;
  return `${formatWeekdayDayMonth(date)} ${time}`;
}

export function formatDayMonth(iso: string): string {
  return dayMonth.format(parseDate(iso));
}

export function formatMonthYear(iso: string): string {
  const text = monthYear.format(parseDate(iso));
  return text.charAt(0).toUpperCase() + text.slice(1);
}
