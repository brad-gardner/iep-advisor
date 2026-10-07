import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { exitReasonLabel } from '../../lib/student-enum-labels';
import { EXIT_REASONS } from '../../types';
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
  const { t } = useTranslation('educator');
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('exitStudent.modalTitle')}
      size="sm"
      data-testid="exit-student-modal"
    >
      <ExitStudentForm studentName={studentName} onClose={onClose} onSubmit={onSubmit} />
    </Modal>
  );
}

function ExitStudentForm({ studentName, onClose, onSubmit }: Omit<ExitStudentModalProps, 'open'>) {
  const { t } = useTranslation(['educator', 'common']);
  const [exitReason, setExitReason] = useState<ExitReason>('Graduated');
  const [exitedAt, setExitedAt] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    const result = await onSubmit({ exitReason, exitedAt: exitedAt || undefined });
    if (!result.success) setError(result.error ?? t('educator:exitStudent.errorDefault'));
    setIsSubmitting(false);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="exit-student-form">
      <p className="text-sm text-brand-slate-600">
        {t('educator:exitStudent.description', { name: studentName })}
      </p>
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Select
        id="exit-student-reason"
        label={t('educator:exitStudent.reasonLabel')}
        value={exitReason}
        onChange={(e) => setExitReason(e.target.value as ExitReason)}
        data-testid="exit-student-reason"
      >
        {EXIT_REASONS.map((reason) => (
          <option key={reason} value={reason}>
            {exitReasonLabel(reason)}
          </option>
        ))}
      </Select>

      <Input
        id="exit-student-date"
        label={t('educator:exitStudent.dateLabel')}
        type="date"
        value={exitedAt}
        onChange={(e) => setExitedAt(e.target.value)}
        data-testid="exit-student-date"
      />

      <div className="flex items-center justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
          {t('common:ui.cancel')}
        </Button>
        <Button type="submit" variant="danger" loading={isSubmitting} data-testid="exit-student-submit">
          {t('educator:exitStudent.submit')}
        </Button>
      </div>
    </form>
  );
}
