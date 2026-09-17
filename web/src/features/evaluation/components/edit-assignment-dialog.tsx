import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { toDateInputValue } from '@/lib/format-date';
import { updateEvaluatorAssignment } from '../api/evaluation-api';
import type { EvaluatorAssignmentDto } from '../types';

interface EditAssignmentDialogProps {
  studentId: number;
  assignment: EvaluatorAssignmentDto;
  onClose: () => void;
  onChanged: (updated: EvaluatorAssignmentDto) => void;
}

/** Edit an evaluator assignment's due date and notes. */
export function EditAssignmentDialog({ studentId, assignment, onClose, onChanged }: EditAssignmentDialogProps) {
  const [dueDate, setDueDate] = useState(() => toDateInputValue(assignment.dueDate));
  const [notes, setNotes] = useState(assignment.notes ?? '');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await updateEvaluatorAssignment(studentId, assignment.id, {
        dueDate: dueDate || undefined,
        notes: notes.trim() || undefined,
      });
      if (res.success && res.data) {
        onChanged(res.data);
        onClose();
      } else {
        setError(res.message ?? 'Could not update the assignment.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not update the assignment.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open
      onClose={onClose}
      preventClose={isSubmitting}
      title={`Edit assignment — ${assignment.domain}`}
      size="sm"
      data-testid="edit-assignment-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} loading={isSubmitting} data-testid="edit-assignment-submit">
            Save
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}
        <Input
          label="Due date"
          type="date"
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          data-testid="edit-assignment-due-date"
        />
        <RichTextEditor
          label="Notes"
          value={notes}
          onChange={setNotes}
          minRows={3}
          maxLength={2000}
          data-testid="edit-assignment-notes"
        />
      </form>
    </Modal>
  );
}
