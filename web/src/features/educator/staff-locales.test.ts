import { describe, expect, it } from 'vitest';
import i18n from '@/lib/i18n';

// Guards the staff/admin namespace split (plan phase 5): a staff namespace's
// English must stay OUT of the main chunk and arrive only once its own
// lazy route chunk loads — this file proves both halves of that with the
// `educator` namespace (`features/educator/staff-locales.ts`). Order within
// this file matters (the "absent" assertion must run before the dynamic
// import below) — Vitest isolates each test FILE into its own module
// registry by default, so no other test file's import of `staff-locales`
// can leak into this one and make the "before" assertion flaky.
describe('staff namespace registration (educator)', () => {
  it('is absent from i18n resources before the staff chunk registers it', () => {
    expect(i18n.hasResourceBundle('en', 'educator')).toBe(false);
  });

  it('is present once the staff chunk (its `staff-locales` entry) is imported', async () => {
    await import('./staff-locales');

    expect(i18n.hasResourceBundle('en', 'educator')).toBe(true);
    expect(i18n.getResourceBundle('en', 'educator')).toEqual({
      studentsPage: {
        caseloadEmptyState:
          "No students on your caseload yet — your school admin can add you to a student's IEP team, or create one.",
      },
    });
  });
});
