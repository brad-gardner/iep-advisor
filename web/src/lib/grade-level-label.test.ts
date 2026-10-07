import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { gradeLevelLabel } from './grade-level-label';

describe('gradeLevelLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('returns an empty string for a null/undefined/empty value', () => {
    expect(gradeLevelLabel(null)).toBe('');
    expect(gradeLevelLabel(undefined)).toBe('');
    expect(gradeLevelLabel('')).toBe('');
  });

  it('translates an already-canonical value', () => {
    expect(gradeLevelLabel('5th')).toBe('5th');
  });

  // The IEP 101/child-form raw values this normalizes before lookup —
  // `common:gradeLevel.*` is keyed by the CANONICAL form ("5th"), not these.
  it.each([
    ['5', '5th'],
    ['K', 'Kindergarten'],
    ['PK', 'Pre-K'],
  ])('normalizes a raw stored value %s to the canonical English label %s', (raw, canonical) => {
    expect(gradeLevelLabel(raw)).toBe(canonical);
  });

  it.each([
    ['5', '5.º grado'],
    ['K', 'Kínder'],
    ['PK', 'Prekínder'],
  ])('normalizes a raw stored value %s to its Spanish label %s', async (raw, expected) => {
    await i18n.changeLanguage('es');
    expect(gradeLevelLabel(raw)).toBe(expected);
  });

  it('passes through a value that matches no known grade level, unchanged', () => {
    expect(gradeLevelLabel('Some Legacy Value')).toBe('Some Legacy Value');
  });
});
