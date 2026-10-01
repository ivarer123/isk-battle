import { describe, expect, it } from 'vitest';
import { t, tryT } from './t';

describe('t', () => {
  it('fills placeholders', () => {
    expect(t('home.members', { count: 3, capacity: 10 })).toBe('3 av 10 medlemmar');
  });

  it('falls back for unknown server keys', () => {
    expect(tryT('error.something_new', 'fallback')).toBe('fallback');
    expect(tryT('period.Week', 'x')).toBe('Vecka');
  });
});
