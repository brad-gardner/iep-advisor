import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { eligibilityOutcomeLabel, evaluationCaseKindLabel, evaluationCaseStatusLabel } from './evaluation-case-label';
// `evaluation` is a staff-only namespace (plan phase 5) — its English isn't
// bundled in `resources` (see `lib/i18n/index.ts`), only registered by this
// side-effect import, exactly as the real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before any page that
// uses it can render.
import '@/app/lazy-routes/staff-locales';
describe('evaluationCaseKindLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every kind in English', () => {
    expect(evaluationCaseKindLabel('Initial')).toBe('Initial evaluation');
    expect(evaluationCaseKindLabel('Reevaluation')).toBe('Reevaluation');
  });

  it('translates every kind in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(evaluationCaseKindLabel('Initial')).toBe('Evaluación inicial');
    expect(evaluationCaseKindLabel('Reevaluation')).toBe('Reevaluación');
  });
});

describe('evaluationCaseStatusLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every status in English', () => {
    expect(evaluationCaseStatusLabel('Open')).toBe('Open');
    expect(evaluationCaseStatusLabel('ConsentPending')).toBe('Consent pending');
    expect(evaluationCaseStatusLabel('InProgress')).toBe('In progress');
    expect(evaluationCaseStatusLabel('Determined')).toBe('Determined');
    expect(evaluationCaseStatusLabel('Closed')).toBe('Closed');
  });

  it('translates every status in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(evaluationCaseStatusLabel('ConsentPending')).toBe('Consentimiento pendiente');
    expect(evaluationCaseStatusLabel('Determined')).toBe('Determinado');
  });
});

describe('eligibilityOutcomeLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every outcome in English', () => {
    expect(eligibilityOutcomeLabel('Eligible')).toBe('Eligible');
    expect(eligibilityOutcomeLabel('NotEligible')).toBe('Not eligible');
    expect(eligibilityOutcomeLabel('Withdrawn')).toBe('Withdrawn');
  });

  it('translates every outcome in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(eligibilityOutcomeLabel('Eligible')).toBe('Elegible');
    expect(eligibilityOutcomeLabel('NotEligible')).toBe('No elegible');
  });
});
