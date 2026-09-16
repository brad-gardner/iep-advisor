import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { OBLIGATION_KIND_LABELS } from '@/features/obligations/types';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { closeEvaluation, createIepFromEtr } from '../api/evaluation-api';
import { useEvaluationCase } from '../hooks/use-evaluation-case';
import { AddAssignmentForm } from './add-assignment-form';
import { ConsentSection } from './consent-section';
import { DetermineDialog } from './determine-dialog';
import { DueDateOverrideDialog } from './due-date-override-dialog';
import { EvaluationTimeline } from './evaluation-timeline';
import { EvaluatorAssignmentsTable } from './evaluator-assignments-table';
import { StartEvaluationForm } from './start-evaluation-form';
import {
  ELIGIBILITY_OUTCOME_LABELS,
  EVALUATION_CASE_KIND_LABELS,
  EVALUATION_CASE_STATUS_LABELS,
} from '../types';
import type { EvaluationCaseDto, EvaluatorAssignmentDto } from '../types';

const CASE_OPEN_FOR_ACTIONS: EvaluationCaseDto['status'][] = ['Open', 'ConsentPending', 'InProgress'];

interface EvaluationCardProps {
  studentId: number;
}

/** Student page "Evaluation" card: referral → consent → clock → determination
 *  → ETR handoff (plan 7, decision 1). No case yet renders the start form;
 *  an existing case renders its timeline, consent capture, due-date override,
 *  evaluator assignments, and the determine/close/create-IEP actions. */
export function EvaluationCard({ studentId }: EvaluationCardProps) {
  const { evaluation, isLoading, error, retry, applyUpdate } = useEvaluationCase(studentId);
  const navigate = useNavigate();
  const [dueDateDialogOpen, setDueDateDialogOpen] = useState(false);
  const [determineOpen, setDetermineOpen] = useState(false);
  const [isClosing, setIsClosing] = useState(false);
  const [isCreatingIep, setIsCreatingIep] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const patchAssignments = (assignments: EvaluatorAssignmentDto[]) => {
    if (evaluation) applyUpdate({ ...evaluation, assignments });
  };

  const handleClose = async () => {
    setIsClosing(true);
    setActionError(null);
    try {
      const res = await closeEvaluation(studentId);
      if (res.success && res.data) applyUpdate(res.data);
      else setActionError(res.message ?? 'Could not close the case.');
    } catch (err) {
      setActionError(apiErrorMessage(err, 'Could not close the case.'));
    } finally {
      setIsClosing(false);
    }
  };

  const handleCreateIep = async () => {
    setIsCreatingIep(true);
    setActionError(null);
    try {
      const res = await createIepFromEtr(studentId);
      if (res.success && res.data) navigate(`/educator/documents/${res.data.instanceId}`);
      else setActionError(res.message ?? 'Could not create the IEP.');
    } catch (err) {
      setActionError(apiErrorMessage(err, 'Could not create the IEP.'));
    } finally {
      setIsCreatingIep(false);
    }
  };

  const isOpenForActions = evaluation != null && CASE_OPEN_FOR_ACTIONS.includes(evaluation.status);

  return (
    <Card data-testid="evaluation-card">
      <h2 className="mb-4 font-serif text-lg text-brand-slate-800">Evaluation</h2>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={retry} data-testid="evaluation-card-retry">
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {!error && isLoading && (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {!error && !isLoading && !evaluation && (
        <StartEvaluationForm studentId={studentId} onStarted={applyUpdate} />
      )}

      {!error && !isLoading && evaluation && (
        <div className="space-y-5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <Badge>{EVALUATION_CASE_STATUS_LABELS[evaluation.status]}</Badge>
              <span className="text-sm text-brand-slate-600">
                {EVALUATION_CASE_KIND_LABELS[evaluation.kind]}
              </span>
            </div>
            {evaluation.obligation && (
              <span className="flex items-center gap-2 text-xs text-brand-slate-500" data-testid="evaluation-obligation-chip">
                <ObligationStatusChip status={evaluation.obligation.status} />
                {OBLIGATION_KIND_LABELS[evaluation.obligation.kind]} · {formatDate(evaluation.obligation.dueDate)}
              </span>
            )}
          </div>

          {actionError && (
            <div role="alert">
              <Notice variant="error" title={actionError} />
            </div>
          )}

          <EvaluationTimeline entries={evaluation.timeline} />

          {isOpenForActions && <ConsentSection studentId={studentId} evaluation={evaluation} onChanged={applyUpdate} />}

          {isOpenForActions && evaluation.determinationDueDate && (
            <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
              <span className="text-brand-slate-600">
                Determination due {formatDate(evaluation.determinationDueDate)}
                {evaluation.dueDateOverrideReason ? ` (${evaluation.dueDateOverrideReason})` : ''}
              </span>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setDueDateDialogOpen(true)}
                data-testid="evaluation-due-date-override-open"
              >
                Override due date
              </Button>
            </div>
          )}

          {evaluation.status !== 'Closed' && (
            <div className="space-y-3">
              <h3 className="text-sm font-medium text-brand-slate-600">Evaluators</h3>
              <EvaluatorAssignmentsTable
                studentId={studentId}
                assignments={evaluation.assignments}
                onChanged={(updated) =>
                  patchAssignments(evaluation.assignments.map((a) => (a.id === updated.id ? updated : a)))
                }
                onRemoved={(id) => patchAssignments(evaluation.assignments.filter((a) => a.id !== id))}
              />
              <AddAssignmentForm
                studentId={studentId}
                onAdded={(added) => patchAssignments([...evaluation.assignments, added])}
              />
            </div>
          )}

          {evaluation.eligibilityOutcome && (
            <div className="rounded-card border border-brand-slate-200 p-3 text-sm" data-testid="evaluation-determination-summary">
              <p className="font-medium text-brand-slate-700">
                {ELIGIBILITY_OUTCOME_LABELS[evaluation.eligibilityOutcome]}
                {evaluation.determinationDate ? ` · ${formatDate(evaluation.determinationDate)}` : ''}
              </p>
              {evaluation.determinationRationale && (
                <p className="mt-1 text-brand-slate-600">{evaluation.determinationRationale}</p>
              )}
            </div>
          )}

          <div className="flex flex-wrap items-center gap-2 border-t border-brand-slate-100 pt-3">
            {isOpenForActions && (
              <Button onClick={() => setDetermineOpen(true)} data-testid="evaluation-determine-open">
                Determine
              </Button>
            )}
            {evaluation.status !== 'Closed' && (
              <Button
                variant="secondary"
                onClick={handleClose}
                loading={isClosing}
                data-testid="evaluation-close"
              >
                Close
              </Button>
            )}
            {evaluation.status === 'Determined' && (
              <Button
                variant="amber"
                onClick={handleCreateIep}
                loading={isCreatingIep}
                data-testid="evaluation-create-iep"
              >
                Create IEP from ETR
              </Button>
            )}
          </div>

          <DueDateOverrideDialog
            open={dueDateDialogOpen}
            studentId={studentId}
            evaluation={evaluation}
            onClose={() => setDueDateDialogOpen(false)}
            onChanged={applyUpdate}
          />
          <DetermineDialog
            open={determineOpen}
            studentId={studentId}
            onClose={() => setDetermineOpen(false)}
            onDetermined={applyUpdate}
          />
        </div>
      )}
    </Card>
  );
}
