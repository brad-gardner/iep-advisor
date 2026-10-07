import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { familyContactMethodLabel, familyContactOutcomeLabel } from './family-contact-label';
// `family-contact` is a staff-only namespace (plan phase 5) — its English
// isn't bundled in `resources` (see `lib/i18n/index.ts`), only registered
// by this side-effect import, exactly as the real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before any page that
// uses it can render.
import '@/features/family-contact/staff-locales';

describe('familyContactMethodLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every method in English', () => {
    expect(familyContactMethodLabel('Email')).toBe('Email');
    expect(familyContactMethodLabel('InPerson')).toBe('In person');
  });

  it('translates every method in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(familyContactMethodLabel('Email')).toBe('Correo electrónico');
    expect(familyContactMethodLabel('Phone')).toBe('Teléfono');
  });
});

describe('familyContactOutcomeLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every outcome in English', () => {
    expect(familyContactOutcomeLabel('Reached')).toBe('Reached');
    expect(familyContactOutcomeLabel('LeftMessage')).toBe('Left a message');
  });

  it('translates every outcome in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(familyContactOutcomeLabel('Reached')).toBe('Contactado');
    expect(familyContactOutcomeLabel('NoAnswer')).toBe('Sin respuesta');
  });
});
