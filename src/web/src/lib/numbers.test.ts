import { describe, expect, it } from 'vitest';
import { decimals, formatOre, formatPercent, parsePercent } from './numbers';

describe('parsePercent', () => {
  it.each([
    ['12,40', 12.4],
    ['-3.15', -3.15],
    ['+5', 5],
    ['−1,5 %', -1.5],
    ['1 000', 1000],
    [',5', 0.5],
    ['0', 0],
  ])('parses %s', (input, expected) => expect(parsePercent(input)).toBe(expected));

  it.each(['', 'abc', '1,2,3', '--1', '12e3', '1.2.3'])('rejects %s', (input) => expect(parsePercent(input)).toBeNull());
});

describe('formatting', () => {
  it('formats percentages the Swedish way', () => {
    expect(formatPercent(12.4).replace(/\s/g, ' ')).toBe('+12,40 %');
    expect(formatPercent(-3.15).replace(/\s/g, ' ')).toBe('−3,15 %');
    expect(formatPercent(0).replace(/\s/g, ' ')).toBe('0,00 %');
  });

  it('formats öre as kronor', () => {
    expect(formatOre(9_900).replace(/\s/g, ' ')).toBe('99 kr');
  });

  it('counts decimals', () => {
    expect(decimals(1.2345)).toBe(4);
    expect(decimals(12)).toBe(0);
  });
});
