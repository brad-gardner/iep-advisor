import type { ColumnSemantic } from '@/features/admin/templates/document-semantics';

/** `_carriedFrom.date` is an ISO `yyyy-MM-dd`; show it the way the Evidence
 *  drawer does. Shared by every row-block read/edit view that shows a
 *  carried-forward provenance chip. */
export function formatCarriedDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
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
