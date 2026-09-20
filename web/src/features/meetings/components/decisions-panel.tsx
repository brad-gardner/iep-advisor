import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { deleteDecision } from '../api/meeting-decisions-api';
import { useDecisionTargets } from '../hooks/use-decision-targets';
import { useMeetingDecisions } from '../hooks/use-meeting-decisions';
import { MEETING_DECISION_OUTCOME_LABELS } from '../types';
import type { MeetingDecisionDto, MeetingDecisionOutcome } from '../types';
import { DecisionForm } from './decision-form';
import { EditDecisionDialog } from './edit-decision-dialog';

const OUTCOME_VARIANT: Record<MeetingDecisionOutcome, 'success' | 'error' | 'warning'> = {
  Agreed: 'success',
  Disagreed: 'error',
  Deferred: 'warning',
};

interface DecisionsPanelProps {
  meetingId: number;
  documentInstanceId: number | null;
  canManage: boolean;
}

/** Structured decisions captured live on a Held/Continued meeting (plan 7,
 *  decision 3). Never applies a decision to the draft itself — that happens
 *  later, by a human, from the editor's "Proposed edits from meetings" panel. */
export function DecisionsPanel({ meetingId, documentInstanceId, canManage }: DecisionsPanelProps) {
  const { decisions, isLoading, error, retry, addDecision, updateDecision, removeDecision } =
    useMeetingDecisions(meetingId);
  const targetOptions = useDecisionTargets(documentInstanceId);
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<MeetingDecisionDto | null>(null);
  const [deleting, setDeleting] = useState<MeetingDecisionDto | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const handleDelete = async () => {
    if (!deleting) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await deleteDecision(deleting.id);
      removeDecision(deleting.id);
      setDeleting(null);
    } catch (err) {
      setDeleteError(apiErrorMessage(err, 'Could not delete this decision.'));
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div data-testid="decisions-panel">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h3 className="text-sm font-medium text-brand-slate-800">Decisions</h3>
        {canManage && !adding && (
          <Button size="sm" variant="secondary" onClick={() => setAdding(true)} data-testid="decision-add-open">
            Add decision
          </Button>
        )}
      </div>

      {adding && (
        <div className="mb-4 rounded-card border border-brand-slate-200 p-3">
          <DecisionForm
            meetingId={meetingId}
            targetOptions={targetOptions}
            onAdded={(decision) => {
              addDecision(decision);
              setAdding(false);
            }}
            onCancel={() => setAdding(false)}
          />
        </div>
      )}

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" className="mt-2" onClick={retry} data-testid="decisions-retry">
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {!error && isLoading && (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {!error && !isLoading && decisions.length === 0 && (
        <p className="text-sm text-brand-slate-500" data-testid="decisions-empty">
          No decisions recorded yet.
        </p>
      )}

      {!error && !isLoading && decisions.length > 0 && (
        <ul className="space-y-2" data-testid="decisions-list">
          {decisions.map((d) => (
            <li
              key={d.id}
              className="rounded-card border border-brand-slate-200 p-3"
              data-testid={`decision-${d.id}`}
            >
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  {d.targetLabel && <p className="text-xs font-medium text-brand-slate-500">{d.targetLabel}</p>}
                  <Markdown content={d.text} className="text-sm text-brand-slate-800" />
                  <p className="mt-1 text-xs text-brand-slate-500">
                    {d.recordedByName ?? 'Staff'} · {formatDate(d.createdAt)}
                    {d.appliedAt ? ' · Applied to draft' : ''}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant={OUTCOME_VARIANT[d.outcome]}>{MEETING_DECISION_OUTCOME_LABELS[d.outcome]}</Badge>
                  {canManage && (
                    <>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => setEditing(d)}
                        data-testid={`decision-edit-${d.id}`}
                      >
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => {
                          setDeleteError(null);
                          setDeleting(d);
                        }}
                        data-testid={`decision-delete-${d.id}`}
                      >
                        Delete
                      </Button>
                    </>
                  )}
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}

      <EditDecisionDialog decision={editing} onClose={() => setEditing(null)} onUpdated={updateDecision} />

      <ConfirmDialog
        open={deleting !== null}
        title="Delete decision"
        message="This permanently removes this recorded decision. This cannot be undone."
        confirmLabel="Delete decision"
        loading={isDeleting}
        error={deleteError}
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
        data-testid="decision-delete-dialog"
      />
    </div>
  );
}
