import { sectionTypeLabel } from '@/lib/section-type-label';

/**
 * Translated label for an ETR section's `sectionType` — delegates to the
 * shared `sectionTypeLabel` helper (`iep-documents:sectionType.*`), which
 * collapsed this file's own former `ETR_SECTION_TYPE_LABELS` map together
 * with the 3 other `SECTION_LABELS` copies (iep-documents, iep-comparison,
 * and this feature's own `etr-analysis-tab.tsx` sidebar). Kept as a thin
 * wrapper so existing imports of `formatSectionTypeLabel` from this module
 * don't need to change.
 */
export function formatSectionTypeLabel(sectionType: string): string {
  return sectionTypeLabel(sectionType, 'full');
}
