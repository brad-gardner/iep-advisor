import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { documentMeetingTypeLabel, meetingDecisionOutcomeLabel, meetingStatusLabel, meetingTypeLabel } from './meeting-labels';
// `meetings-staff` is a staff-only namespace (plan phase 5) — its English
// isn't bundled in `resources` (see `lib/i18n/index.ts`), only registered
// by this side-effect import, exactly as the real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before any page that
// uses `meetingDecisionOutcomeLabel` can render. `meetingTypeLabel`/
// `meetingStatusLabel` need no such import — they're `common:`-namespaced,
// and `common` is eager everywhere.
import '@/app/lazy-routes/staff-locales';
describe('meetingTypeLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every type in English', () => {
    expect(meetingTypeLabel('AnnualReview')).toBe('Annual review');
    expect(meetingTypeLabel('ManifestationDetermination')).toBe('Manifestation determination');
  });

  it('translates every type in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(meetingTypeLabel('AnnualReview')).toBe('Revisión anual');
    expect(meetingTypeLabel('ManifestationDetermination')).toBe('Determinación de manifestación');
  });
});

describe('meetingStatusLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every status in English', () => {
    expect(meetingStatusLabel('Scheduled')).toBe('Scheduled');
    expect(meetingStatusLabel('Cancelled')).toBe('Cancelled');
  });

  it('translates every status in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(meetingStatusLabel('Scheduled')).toBe('Programada');
    expect(meetingStatusLabel('Cancelled')).toBe('Cancelada');
  });
});

describe('documentMeetingTypeLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('normalizes a lowercase_underscore document meeting type to the matching MeetingType translation', () => {
    expect(documentMeetingTypeLabel('annual_review')).toBe('Annual review');
  });

  it('falls back to the raw value for an unrecognized document meeting type', () => {
    expect(documentMeetingTypeLabel('some_future_value')).toBe('some_future_value');
  });

  it('normalizes in Spanish too', async () => {
    await i18n.changeLanguage('es');
    expect(documentMeetingTypeLabel('reevaluation')).toBe('Reevaluación');
  });
});

describe('meetingDecisionOutcomeLabel', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('translates every outcome in English', () => {
    expect(meetingDecisionOutcomeLabel('Agreed')).toBe('Agreed');
    expect(meetingDecisionOutcomeLabel('Disagreed')).toBe('Disagreed');
    expect(meetingDecisionOutcomeLabel('Deferred')).toBe('Deferred');
  });

  it('translates every outcome in Spanish', async () => {
    await i18n.changeLanguage('es');
    expect(meetingDecisionOutcomeLabel('Agreed')).toBe('Acordado');
    expect(meetingDecisionOutcomeLabel('Disagreed')).toBe('En desacuerdo');
  });
});
