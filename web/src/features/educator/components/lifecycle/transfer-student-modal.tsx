import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import type { DistrictSchool } from '@/features/district-admin/types';
import type { TransferStudentRequest } from '../../types';

interface TransferStudentModalProps {
  open: boolean;
  studentName: string;
  currentSchoolId: number;
  schools: DistrictSchool[];
  onClose: () => void;
  onSubmit: (data: TransferStudentRequest) => Promise<{ success: boolean; error?: string }>;
}

// DistrictAdmin-only move between schools in the district.
export function TransferStudentModal({ open, onClose, ...rest }: TransferStudentModalProps) {
  const { t } = useTranslation('educator');
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('transferStudent.modalTitle')}
      size="sm"
      data-testid="transfer-student-modal"
    >
      <TransferStudentForm onClose={onClose} {...rest} />
    </Modal>
  );
}

function TransferStudentForm({
  studentName,
  currentSchoolId,
  schools,
  onClose,
  onSubmit,
}: Omit<TransferStudentModalProps, 'open'>) {
  const { t } = useTranslation(['educator', 'common']);
  const [schoolId, setSchoolId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const targets = schools.filter((s) => s.id !== currentSchoolId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!schoolId) {
      setError(t('educator:transferStudent.selectSchoolRequired'));
      return;
    }
    setIsSubmitting(true);
    const result = await onSubmit({ newSchoolId: Number(schoolId) });
    if (!result.success) setError(result.error ?? t('educator:transferStudent.errorDefault'));
    setIsSubmitting(false);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="transfer-student-form">
      <p className="text-sm text-brand-slate-600">
        {t('educator:transferStudent.description', { name: studentName })}
      </p>
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Select
        id="transfer-student-school"
        label={t('educator:transferStudent.newSchoolLabel')}
        value={schoolId}
        onChange={(e) => setSchoolId(e.target.value)}
        data-testid="transfer-student-school"
      >
        <option value="">{t('educator:transferStudent.selectSchool')}</option>
        {targets.map((school) => (
          <option key={school.id} value={school.id}>
            {school.name}
          </option>
        ))}
      </Select>

      <div className="flex items-center justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
          {t('common:ui.cancel')}
        </Button>
        <Button type="submit" loading={isSubmitting} data-testid="transfer-student-submit">
          {t('educator:transferStudent.submit')}
        </Button>
      </div>
    </form>
  );
}
