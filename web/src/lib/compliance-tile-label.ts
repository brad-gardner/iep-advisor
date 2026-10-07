import type { TFunction } from 'i18next';

// Human-facing labels for the compliance board's six always-anchored tiles
// (`features/district-admin/lib/compliance-tiles.ts`'s
// `COMPLIANCE_SUMMARY_TILES`), shared by the staff home teaser
// (`features/home/components/compliance-summary-block.tsx`) and the full
// board (`features/district-admin/components/compliance-summary-tiles.tsx`,
// `compliance-school-table.tsx`) — translated via
// `district-admin:complianceTiles.<key>` (a staff-only namespace). DISPLAY
// ONLY; the `key` itself (`overdueAnnual`, …) stays the API's/attention
// filter's identifier regardless of language, same shape as `orgRoleLabel`.
//
// Unlike `orgRoleLabel`/`disabilityCategoryLabel` (plain functions calling
// the global `i18n.t` instance directly — needed there because those are
// also called from outside any component), this one takes the CALLER's own
// `t`: every call site here is already inside a component that calls
// `useTranslation`, so a plain `i18n.t` call would be pure loss — it
// doesn't subscribe to anything, so the component wouldn't re-render once
// Spanish actually finishes loading (see `docs/solutions/logic-errors/
// 2026-10-07-i18n-role-split-locales-and-lazy-chunk-boundaries.md`). Passing
// the caller's own `t` gets that re-render for free, same as any other
// translated text in the component. A caller whose own `useTranslation` list
// doesn't start with `district-admin` (e.g. the home teaser's `['home',
// 'district-admin']`) grabs a second, `district-admin`-scoped `t` via its
// own `useTranslation('district-admin')` call just to pass in here — see
// `ComplianceSummaryBlock`.
export function complianceTileLabel(t: TFunction<'district-admin'>, key: string): string {
  return t(`district-admin:complianceTiles.${key}`, { defaultValue: key });
}
