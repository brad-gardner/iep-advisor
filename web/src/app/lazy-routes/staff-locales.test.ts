import { describe, expect, it } from 'vitest';
import i18n from '@/lib/i18n';

// Guards the staff/admin namespace split (plan phase 5): a staff namespace's
// English must stay OUT of the main chunk and arrive only once its own
// lazy route chunk loads (any of the three area barrels, which all import
// this one module) — this file proves both halves of that, once per staff
// namespace discovered on disk, rather than one hand-copied test file per
// namespace (the 6 `features/<feature>/staff-locales.test.ts` files this
// replaced). Order within this file matters (every "absent" assertion must
// run before this module is imported) — Vitest isolates each test FILE into
// its own module registry by default, so no other test file's import of
// `staff-locales` can leak into this one and make an "absent" assertion
// flaky; that's also why every "absent" check runs BEFORE the single
// `await import('./staff-locales')` below, rather than one import per
// namespace (a second dynamic import of an already-evaluated module is a
// cache hit, not a fresh re-run — the registration side effect only
// happens once regardless, which is exactly what makes testing "absent,
// then present, for every namespace at once" the right shape here).
//
// A spot-check per namespace (one real key's value), not a full snapshot of
// each namespace's content — a snapshot here would just duplicate (and
// drift from) the JSON files; `locale-parity.test.ts` already covers their
// actual content exhaustively.
const SPOT_CHECKS: Record<string, { path: string; value: unknown }> = {
  educator: { path: 'studentsPage.title', value: 'Students' },
  evaluation: { path: 'card.heading', value: 'Evaluation' },
  'family-contact': { path: 'card.heading', value: 'Family contact' },
  'meeting-brief': { path: 'page.title', value: 'Meeting brief' },
  'meetings-staff': { path: 'decisionsPanel.heading', value: 'Decisions' },
  obligations: { path: 'card.heading', value: 'Timeline' },
  'document-authoring': { path: 'list.title', value: 'Documents' },
};

function readPath(obj: unknown, path: string): unknown {
  return path.split('.').reduce<unknown>((acc, key) => (acc as Record<string, unknown> | undefined)?.[key], obj);
}

// Discovered the same way `staff-locales.ts` discovers them — so a new
// staff namespace is covered here automatically too, with nothing to add.
const staffNamespaces = Object.keys(
  import.meta.glob('/src/locales/en/staff/*.json', { eager: true })
).map((path) => path.slice(path.lastIndexOf('/') + 1, -'.json'.length));

describe('staff namespace registration (app/lazy-routes/staff-locales)', () => {
  it('found every staff namespace this suite knows a spot-check for (keep SPOT_CHECKS in sync with locales/en/staff/*.json)', () => {
    expect(new Set(staffNamespaces)).toEqual(new Set(Object.keys(SPOT_CHECKS)));
  });

  it.each(staffNamespaces)('%s is absent from i18n resources before the staff chunk registers it', (ns) => {
    expect(i18n.hasResourceBundle('en', ns)).toBe(false);
  });

  it('registers every staff namespace once the staff chunk (its `staff-locales` entry) is imported', async () => {
    await import('./staff-locales');

    for (const ns of staffNamespaces) {
      expect(i18n.hasResourceBundle('en', ns)).toBe(true);
      const check = SPOT_CHECKS[ns];
      expect(readPath(i18n.getResourceBundle('en', ns), check.path)).toBe(check.value);
    }
  });

  // The parent-facing RSVP page uses the separate, EAGER `meetings`
  // namespace (phase 3) — it must never be affected by the staff-only
  // `meetings-staff` registration above.
  it('never touches the eager parent `meetings` namespace', () => {
    expect(i18n.hasResourceBundle('en', 'meetings')).toBe(true);
    expect(i18n.getResourceBundle('en', 'meetings')).not.toHaveProperty('decisionsPanel');
  });
});
