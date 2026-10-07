import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { contributionKindLabel } from './contribution-label';

describe('contributionKindLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every kind in English', () => {
    expect(contributionKindLabel('Strength')).toBe('A strength');
    expect(contributionKindLabel('WorksAtHome')).toBe('What works at home');
    expect(contributionKindLabel('Other')).toBe('Something else');
  });

  it('translates every kind in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(contributionKindLabel('Strength')).toBe('Una fortaleza');
    expect(contributionKindLabel('Priority')).toBe('Una prioridad para este año');
  });
});
