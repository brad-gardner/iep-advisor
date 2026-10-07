import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { obligationKindLabel, obligationStatusLabel } from './obligation-label';
// `obligations` is a staff-only namespace (plan phase 5) — its English isn't
// bundled in `resources` (see `lib/i18n/index.ts`), only registered by this
// side-effect import, exactly as the real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before any page that
// uses it can render.
import '@/features/obligations/staff-locales';

describe('obligationKindLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every kind in English', () => {
    expect(obligationKindLabel('AnnualReview')).toBe('Annual review');
    expect(obligationKindLabel('EtrDue')).toBe('ETR');
    expect(obligationKindLabel('GoalObservationStale')).toBe('Goal progress overdue');
  });

  it('translates every kind in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(obligationKindLabel('AnnualReview')).toBe('Revisión anual');
    expect(obligationKindLabel('EvaluatorSubmission')).toBe('Envío del evaluador');
  });
});

describe('obligationStatusLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every status in English', () => {
    expect(obligationStatusLabel('Upcoming')).toBe('Upcoming');
    expect(obligationStatusLabel('DueSoon')).toBe('Due soon');
    expect(obligationStatusLabel('Overdue')).toBe('Overdue');
    expect(obligationStatusLabel('Unknown')).toBe('Unknown');
  });

  // Spanish must match the API's digest labels (glossary-es.md).
  it('translates every status in Spanish to match the API digest labels', async () => {
    await i18n.changeLanguage('es');
    expect(obligationStatusLabel('Upcoming')).toBe('Próxima');
    expect(obligationStatusLabel('DueSoon')).toBe('Próxima a vencer');
    expect(obligationStatusLabel('Overdue')).toBe('Vencida');
  });
});
