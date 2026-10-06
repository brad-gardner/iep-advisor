import { afterEach, describe, expect, it } from 'vitest';
import i18n from './index';

describe('document language sync', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('sets document.documentElement.lang to the resolved language on changeLanguage', async () => {
    await i18n.changeLanguage('es');
    expect(document.documentElement.lang).toBe('es');

    await i18n.changeLanguage('en');
    expect(document.documentElement.lang).toBe('en');
  });
});
