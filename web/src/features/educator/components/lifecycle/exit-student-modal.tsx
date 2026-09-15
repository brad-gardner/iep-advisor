import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { EXIT_REASONS, EXIT_REASON_LABELS } from '../../types';
import type { ExitReason, ExitStudentRequest } from '../../types';

interface ExitStudentModalProps {
  open: boolean;
  studentName: string;
  onClose: () => void;
  onSubmit: (data: ExitStudentRequest) => Promise<{ success: boolean; error?: string }>;
}

// Exit needs a reason (and optional date), so it is a small form in a Modal
// rather than a bare ConfirmDialog. Children remount per open → fresh state.
export function ExitStudentModal({ open, studentName, onClose, onSubmit }: ExitStudentModalProps) {
  return (
    <Modal open={open} onClose={onClose} title="Exit student" size="sm" data-testid="exit-student-modal">
      <ExitStudentForm studentName={studentName} onClose={onClose} onSubmit={onSubmit} />
    </Modal>
  );
}

function ExitStudentForm({ studentName, onClose, onSubmit }: Omit<ExitStudentModalProps, 'open'>) {
  const [exitReason, setExitReason] = useState<ExitReason>('Graduated');
  const [exitedAt, setExitedAt] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    const result = await onSubmit({ exitReason, exitedAt: exitedAt || undefined });
    if (!result.success) setError(result.error ?? 'Could not exit the student');
    setIsSubmitting(false);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="exit-student-form">
      <p className="text-sm text-brand-slate-600">
        {studentName} will be marked as exited and hidden from the active roster. Documents,
        family links and the IEP team are kept, and the student can be reactivated later.
      </p>
      {error && <Notice variant="error" title={error} />}

      <Select
        id="exit-student-reason"
        label="Reason *"
        value={exitReason}
        onChange={(e) => setExitReason(e.target.value as ExitReason)}
        data-testid="exit-student-reason"
      >
        {EXIT_REASONS.map((reason) => (
          <option key={reason} value={reason}>
            {EXIT_REASON_LABELS[reason]}
          </option>
        ))}
      </Select>

      <Input
        id="exit-student-date"
        label="Exit date"
        type="date"
        value={exitedAt}
        onChange={(e) => setExitedAt(e.target.value)}
        data-testid="exit-student-date"
      />

      <div className="flex items-center justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" variant="danger" loading={isSubmitting} data-testid="exit-student-submit">
          Exit student
        </Button>
      </div>
    </form>
  );
}
