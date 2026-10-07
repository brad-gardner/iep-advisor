import i18n from './i18n';

// Human-facing labels for the compliance board's six always-anchored tiles
// (`features/district-admin/lib/compliance-tiles.ts`'s
// `COMPLIANCE_SUMMARY_TILES`), shared by the staff home teaser
// (`features/home/components/compliance-summary-block.tsx`) and the full
// board (`features/district-admin/components/compliance-summary-tiles.tsx`,
// `compliance-school-table.tsx`) — translated via
// `district-admin:complianceTiles.<key>` (a staff-only namespace; every
// caller renders only behind the lazy staff/district-admin route chunk,
// never eagerly, so this is a safe namespace to use here — see
// `docs/i18n/README.md`'s "Staff and admin namespaces"). DISPLAY ONLY; the
// `key` itself (`overdueAnnual`, …) stays the API's/attention filter's
// identifier regardless of language, same shape as `orgRoleLabel`.
//
// A plain `i18n.t` call doesn't load anything by itself — every caller must
// list `district-admin` in its own `useTranslation` call so the namespace's
// Spanish bundle actually loads and the component re-renders when it
// arrives (see `docs/solutions/logic-errors/
// 2026-10-07-i18n-role-split-locales-and-lazy-chunk-boundaries.md`).
export function complianceTileLabel(key: string): string {
  return i18n.t(`district-admin:complianceTiles.${key}`, { defaultValue: key });
}
