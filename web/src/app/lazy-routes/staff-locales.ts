import { registerEnglishNamespace } from '@/lib/i18n';
import type { EnResources } from '@/lib/i18n/types';

// Registers every staff/admin English namespace found under
// `locales/en/staff/*.json`, by namespace NAME (the filename) — one eager
// `import.meta.glob` instead of one hand-written `import
// '@/features/<feature>/staff-locales'` line per namespace (the 7
// per-feature `staff-locales.ts` files, and this list, this file used to
// need — see the plan phase 5 review that simplified this). Adding a new
// staff/admin namespace is now just "drop the two JSON files" — nothing
// here needs a new line. This module is itself only ever reached through
// one of the three lazy area chunks' own dynamic `import()`
// (`app/routes.tsx`), never statically from the main chunk, so the glob
// being "eager" doesn't put any of this JSON in the main bundle — see
// `docs/i18n/README.md`'s "Staff and admin namespaces" for the full
// reasoning, and `check:bundle`/this file's own test for how that's
// verified.
//
// Imported by all three lazy area chunks (staff, district-admin,
// platform-admin): district and platform admins are staff too, and shared
// staff components (e.g. the educator school filter on the compliance
// board) render across areas.
const staffEnModules = import.meta.glob('/src/locales/en/staff/*.json', { eager: true }) as Record<
  string,
  { default: Record<string, unknown> }
>;

function namespaceOfBasename(path: string): string {
  return path.slice(path.lastIndexOf('/') + 1, -'.json'.length);
}

for (const [path, mod] of Object.entries(staffEnModules)) {
  const ns = namespaceOfBasename(path) as keyof EnResources;
  // `mod.default`'s real shape is whatever `locales/en/staff/<ns>.json`
  // happens to contain — the glob can't narrow it to the specific
  // `EnResources[K]` a literal namespace name would get from a direct
  // `import en<Ns> from '...'` (same reason `lib/i18n/index.ts`'s own
  // `resources.en` assembly isn't literally key-checked either, for the
  // eager parent namespaces). `types.d.ts`'s `EnResources` entry for this
  // namespace is still what makes `useTranslation(ns)` itself strictly
  // typed everywhere it's actually used.
  registerEnglishNamespace(ns, mod.default as EnResources[typeof ns]);
}
