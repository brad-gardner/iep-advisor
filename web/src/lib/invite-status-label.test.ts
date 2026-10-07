import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { inviteStatusLabel } from './invite-status-label';

describe('inviteStatusLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it.each([
    ['Pending', 'Pending'],
    ['Accepted', 'Accepted'],
    ['Declined', 'Declined'],
    ['Tentative', 'Tentative'],
  ] as const)('translates %s to English %s', (status, expected) => {
    expect(inviteStatusLabel(status)).toBe(expected);
  });

  it.each([
    ['Pending', 'Pendiente'],
    ['Accepted', 'Aceptada'],
    ['Declined', 'Rechazada'],
    ['Tentative', 'Tentativa'],
  ] as const)('translates %s to Spanish %s', async (status, expected) => {
    await i18n.changeLanguage('es');
    expect(inviteStatusLabel(status)).toBe(expected);
  });
});
