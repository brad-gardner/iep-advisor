import i18n from './i18n';

export type SectionLabelStyle = 'short' | 'full';

/** `"some_unknown_kind"` → `"Some Unknown Kind"` — same shape as
 *  `EtrSectionCard`'s own `humanizeKey` (for a parsed section's raw field
 *  keys), applied here to an unrecognized section *kind* itself. */
function humanizeSectionType(kind: string): string {
  return kind
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\b\w/g, (c) => c.toUpperCase())
    .trim();
}

/**
 * Human-facing label for an IEP/ETR analysis "section kind" (the small,
 * stable backend string like `"present_levels"` or `"referral_reason"`) —
 * for DISPLAY ONLY. Collapses what were 4 separate local `SECTION_LABELS`
 * maps (iep-documents' analysis-tab.tsx sidebar + analysis-section-detail.tsx
 * heading, iep-comparison's section-diff.tsx, and etr-documents'
 * etr-analysis-tab.tsx sidebar) into one translated lookup. `EtrSectionCard`
 * (etr-documents) used to reach this through its own thin
 * `formatSectionTypeLabel` wrapper (`lib/section-type-labels.ts`); that
 * wrapper added nothing over calling this helper directly and was removed.
 *
 * IEP and ETR section kinds don't collide (both only share `"other"`), so
 * both live in `iep-documents:sectionType.*` — the namespace of 2 of the
 * former 4 copies — rather than `common` (a new key there is outside this
 * phase's assignment; see docs/i18n/README.md). A caller outside
 * iep-documents must include `'iep-documents'` in its own `useTranslation`
 * array so the namespace's Spanish data is loaded before this plain
 * function (not a hook) is called from render.
 *
 * `style: 'short'` is the former iep-documents/analysis-tab.tsx and
 * etr-documents/etr-analysis-tab.tsx sidebar-nav wording (e.g.
 * `"Transition"`); `style: 'full'` is the former
 * analysis-section-detail.tsx/section-diff.tsx heading wording (e.g.
 * `"Transition Planning"`). Most ETR-only kinds DO still have one wording
 * (`full`/`short` happen to match), but not all: `occupational_physical_therapy`
 * is `"OT/PT"` short and `"Occupational/Physical Therapy"` full, same as any
 * shared kind (an improvement over the previous behavior, where
 * `AnalysisSectionDetail`'s heading fell through to the raw `sectionType`
 * string for any ETR section, since its own `SECTION_LABELS` never had ETR
 * keys). An unrecognized kind falls back to a humanized, title-cased version
 * of the raw string (`"some_unknown_kind"` → `"Some Unknown Kind"`) rather
 * than the raw `snake_case` value, same idea as `EtrSectionCard`'s own
 * `humanizeKey` for a parsed section's field keys.
 */
export function sectionTypeLabel(kind: string, style: SectionLabelStyle = 'full'): string {
  return i18n.t(`iep-documents:sectionType.${style}.${kind}`, { defaultValue: humanizeSectionType(kind) });
}
