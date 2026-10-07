import i18n from './i18n';

export type SectionLabelStyle = 'short' | 'full';

/**
 * Human-facing label for an IEP/ETR analysis "section kind" (the small,
 * stable backend string like `"present_levels"` or `"referral_reason"`) —
 * for DISPLAY ONLY. Collapses what were 4 separate local `SECTION_LABELS`
 * maps (iep-documents' analysis-tab.tsx sidebar + analysis-section-detail.tsx
 * heading, iep-comparison's section-diff.tsx, and etr-documents'
 * etr-analysis-tab.tsx sidebar) into one translated lookup, plus
 * etr-documents' own `formatSectionTypeLabel` (lib/section-type-labels.ts).
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
 * `"Transition Planning"`). ETR kinds only ever had one wording, so `full`
 * and `short` match for those (an improvement over the previous behavior,
 * where `AnalysisSectionDetail`'s heading fell through to the raw
 * `sectionType` string for any ETR section, since its own `SECTION_LABELS`
 * never had ETR keys). An unrecognized kind falls back to the raw string,
 * same as every prior call site.
 */
export function sectionTypeLabel(kind: string, style: SectionLabelStyle = 'full'): string {
  return i18n.t(`iep-documents:sectionType.${style}.${kind}`, { defaultValue: kind });
}
