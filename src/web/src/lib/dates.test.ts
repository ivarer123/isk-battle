import { describe, expect, it } from 'vitest';
import { formatStockholmDateTime } from './dates';

describe('formatStockholmDateTime', () => {
  const today = '2026-10-06'; // Tuesday

  it('says ikväll for this evening and idag for earlier today', () => {
    expect(formatStockholmDateTime('2026-10-06T22:30:00+02:00', today)).toBe('ikväll kl. 22.30');
    expect(formatStockholmDateTime('2026-10-06T09:00:00+02:00', today)).toBe('idag kl. 09.00');
  });

  it('says imorgon for tomorrow', () => {
    expect(formatStockholmDateTime('2026-10-07T07:00:00Z', today)).toBe('imorgon kl. 09.00');
  });

  it('uses weekday and date further away', () => {
    expect(formatStockholmDateTime('2026-10-09T22:30:00+02:00', today)).toBe('fredag 9 oktober kl. 22.30');
  });

  it('shows a midnight close as 23.59 the day before', () => {
    expect(formatStockholmDateTime('2026-10-12T00:00:00+02:00', '2026-10-10')).toBe('imorgon kl. 23.59');
    expect(formatStockholmDateTime('2026-10-12T00:00:00+02:00', '2026-10-09')).toBe('söndag 11 oktober kl. 23.59');
  });

  it('uses Stockholm time in winter', () => {
    expect(formatStockholmDateTime('2026-12-31T23:00:00Z', '2026-12-31')).toBe('ikväll kl. 23.59');
  });
});
