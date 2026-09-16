import { useState } from 'react';
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
      else setError(res.message ?? 'Could not mark this assignment submitted.');
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not mark this assignment submitted.'));
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
      setError(apiErrorMessage(err, 'Could not remove this assignment.'));
    } finally {
      setIsRemoving(false);
    }
  };

  const columns: TableColumn<EvaluatorAssignmentDto>[] = [
    { key: 'domain', header: 'Domain', cell: (a) => a.domain },
    { key: 'evaluator', header: 'Evaluator', cell: (a) => a.displayName },
    { key: 'dueDate', header: 'Due date', cell: (a) => formatDate(a.dueDate) },
    {
      key: 'status',
      header: 'Status',
      cell: (a) =>
        a.submittedAt ? (
          <span className="inline-flex items-center gap-1 text-brand-teal-600">
            <CheckCircle className="h-3.5 w-3.5" aria-hidden="true" />
            Submitted {formatDate(a.submittedAt)}
          </span>
        ) : a.isOverdue ? (
          <span className="text-brand-danger-700">Overdue</span>
        ) : (
          <span className="text-brand-slate-500">Pending</span>
        ),
    },
    { key: 'notes', header: 'Notes', cell: (a) => a.notes ?? '—' },
  ];

  return (
    <div className="space-y-3" data-testid="evaluator-assignments-table-wrap">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Table
        label="Evaluator assignments"
        data-testid="evaluator-assignments-table"
        columns={columns}
        rows={assignments}
        rowKey={(a) => a.id}
        rowActions={(a) => [
          ...(a.submittedAt
            ? []
            : [
                {
                  label: submittingIds.has(a.id) ? 'Marking…' : 'Mark submitted',
                  onSelect: () => void markSubmitted(a),
                  disabled: submittingIds.has(a.id),
                  'data-testid': `evaluator-assignment-submit-${a.id}`,
                },
              ]),
          {
            label: 'Edit',
            onSelect: () => setEditing(a),
            'data-testid': `evaluator-assignment-edit-${a.id}`,
          },
          {
            label: 'Remove',
            variant: 'danger' as const,
            onSelect: () => setRemoving(a),
            'data-testid': `evaluator-assignment-remove-${a.id}`,
          },
        ]}
        rowActionLabel={(a) => `${a.domain} evaluator`}
        empty={<EmptyState title="No evaluators assigned yet" />}
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
        title="Remove evaluator assignment"
        message={`Remove ${removing?.displayName ?? 'this evaluator'} from ${removing?.domain ?? 'this domain'}?`}
        confirmLabel="Remove"
        loading={isRemoving}
        onConfirm={handleRemove}
        onCancel={() => setRemoving(null)}
        data-testid="evaluator-assignment-remove-dialog"
      />
    </div>
  );
}
