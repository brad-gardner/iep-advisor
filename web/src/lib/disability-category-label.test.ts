import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { disabilityCategoryLabel } from './disability-category-label';

describe('disabilityCategoryLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('returns an empty string for a null/undefined/empty value', () => {
    expect(disabilityCategoryLabel(null)).toBe('');
    expect(disabilityCategoryLabel(undefined)).toBe('');
    expect(disabilityCategoryLabel('')).toBe('');
  });

  it('translates "Specific Learning Disability" (title case) in English', () => {
    expect(disabilityCategoryLabel('Specific Learning Disability')).toBe('Specific learning disability');
  });

  it('translates "Specific Learning Disability" (title case, the server\'s own display string) in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(disabilityCategoryLabel('Specific Learning Disability')).toBe('Discapacidad específica del aprendizaje');
  });

  it('translates the IDEA code abbreviation "SLD" in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(disabilityCategoryLabel('SLD')).toBe('Discapacidad específica del aprendizaje');
  });

  it('is case-insensitive', async () => {
    await i18n.changeLanguage('es');
    expect(disabilityCategoryLabel('specific learning disability')).toBe('Discapacidad específica del aprendizaje');
  });

  it('passes through a value that matches no known IDEA category, unchanged', () => {
    expect(disabilityCategoryLabel('Some Legacy Value')).toBe('Some Legacy Value');
  });
});
