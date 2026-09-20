import { useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { buildFieldLocationLookup } from '@/features/draft-sharing/lib/field-lookup';
import { MEETING_DECISION_OUTCOME_LABELS } from '@/features/meetings/types';
import type { MeetingDecisionOutcome, ProposedEditDto } from '@/features/meetings/types';
import { formatDate } from '@/lib/format-date';
import { useProposedEdits } from '../hooks/use-proposed-edits';
import { jumpToFieldWhenVisible } from '../lib/section-dom';
import { fieldElementId } from './field-renderers/types';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

const OUTCOME_VARIANT: Record<MeetingDecisionOutcome, 'success' | 'error' | 'warning'> = {
  Agreed: 'success',
  Disagreed: 'error',
  Deferred: 'warning',
};

interface ProposedEditsPanelProps {
  instanceId: number;
  templateVersion: TemplateVersionDetailDto;
}

/**
 * Decisions recorded on meetings linked to this draft (or the same student's
 * recent meetings), surfaced for the editor to act on (plan 7, decision 3).
 * Collapsible so it doesn't compete with the sections when there's nothing new;
 * never edits a field itself — "Jump to field" scrolls/focuses it and "Mark
 * applied" only records that a human already made the change by hand.
 */
export function ProposedEditsPanel({ instanceId, templateVersion }: ProposedEditsPanelProps) {
  const { edits, isLoading, error, retry, markApplied, markingId } = useProposedEdits(instanceId);
  const fieldLookup = useMemo(() => buildFieldLocationLookup(templateVersion), [templateVersion]);

  const jumpTo = (edit: ProposedEditDto) => {
    const loc = edit.targetFieldKey ? fieldLookup.get(edit.targetFieldKey) : undefined;
    if (!loc) return;
    jumpToFieldWhenVisible(fieldElementId(loc.fieldId), loc.sectionId);
  };

  return (
    <details className="rounded-card border border-brand-slate-200 p-4" data-testid="proposed-edits-panel">
      <summary className="cursor-pointer text-sm font-medium text-brand-slate-800">
        Proposed edits from meetings{edits.length > 0 ? ` (${edits.length})` : ''}
      </summary>

      <div className="mt-3 space-y-3">
        {error && (
          <div role="alert">
            <Notice variant="error" title={error}>
              <Button size="sm" variant="secondary" className="mt-2" onClick={retry} data-testid="proposed-edits-retry">
                Try again
              </Button>
            </Notice>
          </div>
        )}

        {!error && isLoading && <p className="text-sm text-brand-slate-500">Loading…</p>}

        {!error && !isLoading && edits.length === 0 && (
          <p className="text-sm text-brand-slate-500" data-testid="proposed-edits-empty">
            No decisions from meetings yet.
          </p>
        )}

        {!error && !isLoading && edits.length > 0 && (
          <ul className="space-y-2">
            {edits.map((edit) => (
              <li
                key={edit.decisionId}
                className="rounded-card border border-brand-slate-100 p-3"
                data-testid={`proposed-edit-${edit.decisionId}`}
              >
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <p className="text-xs text-brand-slate-500">
                      {edit.meetingTitle} · {formatDate(edit.recordedAt)}
                    </p>
                    {edit.targetLabel && (
                      <p className="text-xs font-medium text-brand-slate-600">{edit.targetLabel}</p>
                    )}
                    <p className="text-sm text-brand-slate-800">{edit.text}</p>
                  </div>
                  <Badge variant={OUTCOME_VARIANT[edit.outcome]}>{MEETING_DECISION_OUTCOME_LABELS[edit.outcome]}</Badge>
                </div>
                <div className="mt-2 flex items-center gap-2">
                  {edit.targetFieldKey && fieldLookup.has(edit.targetFieldKey) && (
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => jumpTo(edit)}
                      data-testid={`proposed-edit-jump-${edit.decisionId}`}
                    >
                      Jump to field
                    </Button>
                  )}
                  {edit.appliedAt ? (
                    <span className="text-xs text-brand-slate-500" data-testid={`proposed-edit-applied-${edit.decisionId}`}>
                      Applied {formatDate(edit.appliedAt)}
                    </span>
                  ) : (
                    <Button
                      size="sm"
                      variant="secondary"
                      loading={markingId === edit.decisionId}
                      onClick={() => markApplied(edit.decisionId)}
                      data-testid={`proposed-edit-mark-applied-${edit.decisionId}`}
                    >
                      Mark applied
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </details>
  );
}
