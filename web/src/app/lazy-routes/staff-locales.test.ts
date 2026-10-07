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
// Checks that the REGISTERED bundle deep-equals the actual on-disk JSON
// (and that the JSON itself isn't accidentally empty) — not a hand-picked
// spot-check value per namespace (the previous `SPOT_CHECKS` map), which
// needed a new entry, by hand, every time a namespace was added. Reading
// the same files `staff-locales.ts` itself globs, rather than hand-copying
// one expected value per namespace, means adding a new staff namespace
// needs nothing added HERE either — see `docs/i18n/README.md`'s "Adding a
// new staff/admin namespace" steps. This doesn't duplicate
// `locale-parity.test.ts` (which checks en/es key-shape PARITY across every
// namespace, staff or not) — this file is the one place that proves the
// registration MECHANISM itself (timing + content fidelity), independent of
// what any particular key says.
const staffEnModules = import.meta.glob('/src/locales/en/staff/*.json', { eager: true }) as Record<
  string,
  { default: Record<string, unknown> }
>;

function namespaceOfBasename(path: string): string {
  return path.slice(path.lastIndexOf('/') + 1, -'.json'.length);
}

// Discovered the same way `staff-locales.ts` discovers them — so a new
// staff namespace is covered here automatically too, with nothing to add.
const staffNamespaces = Object.keys(staffEnModules).map(namespaceOfBasename);

describe('staff namespace registration (app/lazy-routes/staff-locales)', () => {
  it('found at least one staff namespace to guard (sanity check the glob itself)', () => {
    expect(staffNamespaces.length).toBeGreaterThan(0);
  });

  it.each(staffNamespaces)('%s is absent from i18n resources before the staff chunk registers it', (ns) => {
    expect(i18n.hasResourceBundle('en', ns)).toBe(false);
  });

  it('registers every staff namespace, with content matching its on-disk JSON exactly, once the staff chunk (its `staff-locales` entry) is imported', async () => {
    await import('./staff-locales');

    for (const [path, mod] of Object.entries(staffEnModules)) {
      const ns = namespaceOfBasename(path);
      expect(Object.keys(mod.default).length).toBeGreaterThan(0); // the fixture file itself isn't accidentally empty
      expect(i18n.hasResourceBundle('en', ns)).toBe(true);
      expect(i18n.getResourceBundle('en', ns)).toEqual(mod.default);
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
