import type { AdvocateCitation } from '../types/advocate';

/** DOM id of one goal's card on the IEP page — `#goal-340` scrolls to it. */
export const goalAnchorId = (goalId: number) => `goal-${goalId}`;

/** Where a `goal` citation lands when the server did not say which IEP it belongs to. */
const goalsFallback = (childId: number) => `/children/${childId}/goals`;

/**
 * The route a citation chip opens, or null for kinds with no page of their
 * own (a comparison result, an unknown kind). Ids come from the server's
 * `ReturnedRefs`, never from the model, so they can be trusted to belong to
 * this child; the route still only opens what the API lets the viewer read.
 */
export function citationHref(citation: AdvocateCitation, childId: number): string | null {
  const { id, parent } = citation;
  const parentId = parent && Number.isInteger(parent.id) && parent.id > 0 ? parent.id : null;
  if (!Number.isInteger(id) || id <= 0) return null;

  switch (citation.kind) {
    case 'kb':
      return `/knowledge-base/${id}`;
    case 'child':
    case 'contribution':
    case 'advocacy_goal':
    case 'meeting':
      return `/children/${childId}/overview`;
    case 'iep':
      return `/children/${childId}/ieps/${id}`;
    case 'etr':
      return `/children/${childId}/etrs/${id}`;
    case 'progress_report':
      // The viewer route needs the IEP the report hangs off; without it the
      // IEP list is the closest page that can show the report.
      return parentId != null ? `/children/${childId}/ieps/${parentId}/progress-reports/${id}` : `/children/${childId}/ieps`;
    case 'authored_version':
      return `/children/${childId}/authored-versions/${id}`;
    case 'shared_draft':
      return `/children/${childId}/shared-drafts/${id}`;
    case 'iep_analysis':
    case 'etr_analysis':
    case 'progress_report_analysis':
    case 'analysis_run':
      // Every analysis kind lands on the analysis tab. A progress_report_analysis
      // parent is the progress_report itself, which is not enough to build the
      // report viewer route (that needs the IEP id) — so it goes here too.
      return `/children/${childId}/analysis`;
    case 'iep_section':
      // The IEP page shows the PDF and per-type analysis, not per-section
      // anchors, so a section lands on its document.
      return parentId != null ? `/children/${childId}/ieps/${parentId}` : `/children/${childId}/ieps`;
    case 'etr_section':
      return parentId != null ? `/children/${childId}/etrs/${parentId}` : `/children/${childId}/etrs`;
    case 'goal':
      return parentId != null ? `/children/${childId}/ieps/${parentId}#${goalAnchorId(id)}` : goalsFallback(childId);
    case 'goal_record':
      return goalsFallback(childId);
    case 'journal':
      return `/children/${childId}/journal?entry=${id}`;
    case 'meeting_prep':
    case 'prep_question':
      // A parent's own question has no page of its own; the meeting-prep tab lists them.
      return `/children/${childId}/meeting-prep`;
    case 'comparison':
    default:
      return null;
  }
}

/** Chip text: the server's label, else a plain fallback that never shows a raw kind token. */
export function citationLabel(citation: AdvocateCitation): string {
  if (citation.label && citation.label.trim()) return citation.label.trim();
  switch (citation.kind) {
    case 'kb':
      return `Knowledge base #${citation.id}`;
    case 'child':
      return 'Child profile';
    case 'comparison':
      return 'IEP comparison';
    default:
      return `Source #${citation.id}`;
  }
}

/**
 * Where an `open_goal` suggestion leads: the goal's spot on its IEP when the
 * same answer cited that goal with its IEP, otherwise the goals tab.
 */
export function openGoalHref(goalId: number | null | undefined, citations: AdvocateCitation[], childId: number): string {
  if (typeof goalId === 'number') {
    const cited = citations.find((c) => c.kind === 'goal' && c.id === goalId && c.parent);
    if (cited) {
      const href = citationHref(cited, childId);
      if (href) return href;
    }
  }
  return goalsFallback(childId);
}
