import { describe, expect, it } from 'vitest';
import i18n from '@/lib/i18n';

// Guards the staff/admin namespace split (plan phase 5) for `meeting-brief`,
// the same way `features/educator/staff-locales.test.ts` guards `educator`:
// its English must stay OUT of the main chunk and arrive only once its own
// lazy route chunk loads. Order within this file matters (the "absent"
// assertion must run before the dynamic import below) — Vitest isolates
// each test FILE into its own module registry by default, so no other test
// file's import of `staff-locales` can leak into this one.
describe('staff namespace registration (meeting-brief)', () => {
  it('is absent from i18n resources before the staff chunk registers it', () => {
    expect(i18n.hasResourceBundle('en', 'meeting-brief')).toBe(false);
  });

  it('is present once the staff chunk (its `staff-locales` entry) is imported', async () => {
    await import('./staff-locales');

    expect(i18n.hasResourceBundle('en', 'meeting-brief')).toBe(true);
    expect(i18n.getResourceBundle('en', 'meeting-brief').page.title).toBe('Meeting brief');
  });
});
