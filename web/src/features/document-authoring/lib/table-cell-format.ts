import i18n from '@/lib/i18n';
import { formatDate } from '@/lib/format-date';
import type { ColumnSemantic } from '@/features/admin/templates/document-semantics';

/** `_carriedFrom.date` is an ISO `yyyy-MM-dd`; show it the way the Evidence
 *  drawer does. Shared by every row-block read/edit view that shows a
 *  carried-forward provenance chip. Delegates to the shared `formatDate`
 *  (active-language-aware), rather than a bare `toLocaleDateString()`, so
 *  this follows the active language like every other displayed date. */
export function formatCarriedDate(iso: string): string {
  return formatDate(iso, iso);
}

/** A service's start–end dates for display: "Oct 12, 2026 – Oct 11, 2027"
 *  when both are set, one-sided phrasing when only one is, else "Not set"
 *  (`common:ui.notSet` — the same generic fallback every other field uses). */
export function formatDateRange(start: string | undefined, end: string | undefined): string {
  const s = start ? formatDate(start) : null;
  const e = end ? formatDate(end) : null;
  if (s && e) return `${s} – ${e}`;
  if (s) return i18n.t('document-authoring:dateRange.from', { date: s });
  if (e) return i18n.t('document-authoring:dateRange.through', { date: e });
  return i18n.t('common:ui.notSet');
}

/** Columns whose content is prose and deserves a full-width multiline input. */
export function isLongColumn(semantic: ColumnSemantic | undefined): boolean {
  return (
    semantic === 'goalText' ||
    semantic === 'baseline' ||
    semantic === 'targetCriteria' ||
    semantic === 'findings' ||
    semantic === 'transitionServices' ||
    semantic === 'accommodation'
  );
}

/** Accommodation, transition-services and goal statement/baseline/target
 *  prose get the larger auto-growing rich text editor (plan 2026-10-02-002,
 *  Phases 2-3); stored as a markdown string in the same cell either way. */
export function isRichTextColumn(semantic: ColumnSemantic | undefined): boolean {
  return (
    semantic === 'accommodation' ||
    semantic === 'transitionServices' ||
    semantic === 'goalText' ||
    semantic === 'baseline' ||
    semantic === 'targetCriteria'
  );
}
