import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle } from 'lucide-react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { Table, type TableColumn } from '@/components/ui/table';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { removeEvaluatorAssignment, updateEvaluatorAssignment } from '../api/evaluation-api';
import { EditAssignmentDialog } from './edit-assignment-dialog';
import type { EvaluatorAssignmentDto } from '../types';

interface EvaluatorAssignmentsTableProps {
  studentId: number;
  assignments: EvaluatorAssignmentDto[];
  onChanged: (updated: EvaluatorAssignmentDto) => void;
  onRemoved: (assignmentId: number) => void;
}

/** Evaluator assignments: domain, evaluator, due date, submitted status, plus
 *  per-row "Mark submitted" / "Edit notes" / "Remove". Pending state is
 *  item-scoped (a `Set`/`Map` keyed by assignment id) so independent rows can
 *  act concurrently without blocking each other. */
export function EvaluatorAssignmentsTable({ studentId, assignments, onChanged, onRemoved }: EvaluatorAssignmentsTableProps) {
  const { t } = useTranslation('evaluation');
  const [editing, setEditing] = useState<EvaluatorAssignmentDto | null>(null);
  const [removing, setRemoving] = useState<EvaluatorAssignmentDto | null>(null);
  const [submittingIds, setSubmittingIds] = useState<ReadonlySet<number>>(new Set());
  const [isRemoving, setIsRemoving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const markSubmitted = async (assignment: EvaluatorAssignmentDto) => {
    setError(null);
    setSubmittingIds((cur) => new Set(cur).add(assignment.id));
    try {
      const res = await updateEvaluatorAssignment(studentId, assignment.id, {
        submittedAt: new Date().toISOString(),
      });
      if (res.success && res.data) onChanged(res.data);
      else setError(res.message ?? t('evaluatorAssignmentsTable.markSubmittedFailed'));
    } catch (err) {
      setError(apiErrorMessage(err, t('evaluatorAssignmentsTable.markSubmittedFailed')));
    } finally {
      setSubmittingIds((cur) => {
        const next = new Set(cur);
        next.delete(assignment.id);
        return next;
      });
    }
  };

  const handleRemove = async () => {
    if (!removing) return;
    setIsRemoving(true);
    setError(null);
    try {
      await removeEvaluatorAssignment(studentId, removing.id);
      onRemoved(removing.id);
      setRemoving(null);
    } catch (err) {
      setError(apiErrorMessage(err, t('evaluatorAssignmentsTable.removeFailed')));
    } finally {
      setIsRemoving(false);
    }
  };

  const columns: TableColumn<EvaluatorAssignmentDto>[] = [
    { key: 'domain', header: t('evaluatorAssignmentsTable.domainHeader'), cell: (a) => a.domain },
    { key: 'evaluator', header: t('evaluatorAssignmentsTable.evaluatorHeader'), cell: (a) => a.displayName },
    { key: 'dueDate', header: t('evaluatorAssignmentsTable.dueDateHeader'), cell: (a) => formatDate(a.dueDate) },
    {
      key: 'status',
      header: t('evaluatorAssignmentsTable.statusHeader'),
      cell: (a) =>
        a.submittedAt ? (
          <span className="inline-flex items-center gap-1 text-brand-teal-600">
            <CheckCircle className="h-3.5 w-3.5" aria-hidden="true" />
            {t('evaluatorAssignmentsTable.submittedOn', { date: formatDate(a.submittedAt) })}
          </span>
        ) : a.isOverdue ? (
          <span className="text-brand-danger-700">{t('evaluatorAssignmentsTable.overdue')}</span>
        ) : (
          <span className="text-brand-slate-500">{t('evaluatorAssignmentsTable.pending')}</span>
        ),
    },
    { key: 'notes', header: t('evaluatorAssignmentsTable.notesHeader'), cell: (a) => a.notes ?? '—' },
  ];

  return (
    <div className="space-y-3" data-testid="evaluator-assignments-table-wrap">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Table
        label={t('evaluatorAssignmentsTable.label')}
        data-testid="evaluator-assignments-table"
        columns={columns}
        rows={assignments}
        rowKey={(a) => a.id}
        rowActions={(a) => [
          ...(a.submittedAt
            ? []
            : [
                {
                  label: submittingIds.has(a.id)
                    ? t('evaluatorAssignmentsTable.marking')
                    : t('evaluatorAssignmentsTable.markSubmitted'),
                  onSelect: () => void markSubmitted(a),
                  disabled: submittingIds.has(a.id),
                  'data-testid': `evaluator-assignment-submit-${a.id}`,
                },
              ]),
          {
            label: t('evaluatorAssignmentsTable.editAction'),
            onSelect: () => setEditing(a),
            'data-testid': `evaluator-assignment-edit-${a.id}`,
          },
          {
            label: t('evaluatorAssignmentsTable.removeAction'),
            variant: 'danger' as const,
            onSelect: () => setRemoving(a),
            'data-testid': `evaluator-assignment-remove-${a.id}`,
          },
        ]}
        rowActionLabel={(a) => t('evaluatorAssignmentsTable.rowActionLabel', { domain: a.domain })}
        empty={<EmptyState title={t('evaluatorAssignmentsTable.emptyTitle')} />}
      />

      {editing && (
        <EditAssignmentDialog
          studentId={studentId}
          assignment={editing}
          onClose={() => setEditing(null)}
          onChanged={(updated) => {
            onChanged(updated);
            setEditing(null);
          }}
        />
      )}

      <ConfirmDialog
        open={removing != null}
        title={t('evaluatorAssignmentsTable.removeDialogTitle')}
        message={t('evaluatorAssignmentsTable.removeDialogMessage', {
          name: removing?.displayName ?? t('evaluatorAssignmentsTable.defaultEvaluatorName'),
          domain: removing?.domain ?? t('evaluatorAssignmentsTable.defaultDomain'),
        })}
        confirmLabel={t('evaluatorAssignmentsTable.removeDialogConfirm')}
        loading={isRemoving}
        onConfirm={handleRemove}
        onCancel={() => setRemoving(null)}
        data-testid="evaluator-assignment-remove-dialog"
      />
    </div>
  );
}
