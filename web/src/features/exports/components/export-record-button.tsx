import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { enqueueStudentExport } from '../api/exports-api';

interface ExportRecordButtonProps {
  studentId: number;
}

/** Student page "Export record" action (plan 7, decision 8): enqueues a
 *  student-scoped export job, toasts confirmation, and reveals a link to the
 *  exports page — where the job appears (Download once Completed). */
export function ExportRecordButton({ studentId }: ExportRecordButtonProps) {
  const { t } = useTranslation('exports');
  const { show: showToast } = useToast();
  const [isRequesting, setIsRequesting] = useState(false);
  const [requested, setRequested] = useState(false);

  const handleClick = async () => {
    setIsRequesting(true);
    try {
      const res = await enqueueStudentExport(studentId);
      if (res.success) {
        setRequested(true);
        showToast({ message: t('recordButton.requestedToast'), variant: 'success' });
      } else {
        showToast({ message: res.message ?? t('recordButton.requestFailed'), variant: 'error' });
      }
    } catch (err) {
      showToast({ message: apiErrorMessage(err, t('recordButton.requestFailed')), variant: 'error' });
    } finally {
      setIsRequesting(false);
    }
  };

  return (
    <div className="space-y-2">
      <Button
        variant="secondary"
        className="w-full"
        onClick={handleClick}
        loading={isRequesting}
        data-testid="export-record"
      >
        {t('recordButton.label')}
      </Button>
      {requested && (
        <Link
          to="/educator/admin/exports"
          className="block text-center text-sm text-brand-teal-600 underline"
          data-testid="export-record-view-link"
        >
          {t('recordButton.viewExports')}
        </Link>
      )}
    </div>
  );
}
