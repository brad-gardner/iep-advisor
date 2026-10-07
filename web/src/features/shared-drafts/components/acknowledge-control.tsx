import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { acknowledgeSharedDraft } from '../api/shared-drafts-api';

interface AcknowledgeControlProps {
  revisionId: number;
  acknowledgedAt: string | null;
  onAcknowledged: (acknowledgedAt: string) => void;
}

/** "I've reviewed this" stamp — explicitly labelled as neither consent nor a
 *  signature. Idempotent server-side, so a double-click can't double-record. */
export function AcknowledgeControl({ revisionId, acknowledgedAt, onAcknowledged }: AcknowledgeControlProps) {
  const { t } = useTranslation('shared-drafts');
  const { show: showToast } = useToast();
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (acknowledgedAt) {
    return (
      <p className="flex items-center gap-2 text-sm text-brand-teal-600" data-testid="draft-acknowledged-stamp">
        <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden="true" />
        {t('acknowledge.stamp', { date: formatDate(acknowledgedAt) })}
      </p>
    );
  }

  // Click-triggered (never a mount effect), so translating inline here is
  // safe — no stale-`t`-in-a-fetch-dependency-array concern (see `useHome`'s
  // comment for the case where that WOULD matter).
  const handleClick = async () => {
    setIsSaving(true);
    setError(null);
    try {
      const res = await acknowledgeSharedDraft(revisionId);
      if (res.success && res.data) {
        onAcknowledged(res.data.acknowledgedAt ?? new Date().toISOString());
        showToast({ message: t('acknowledge.toast'), variant: 'success' });
      } else {
        setError(res.message || t('acknowledge.error'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('acknowledge.error')));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-1.5">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <Button size="sm" variant="secondary" onClick={handleClick} loading={isSaving} data-testid="mark-reviewed-button">
        {t('acknowledge.button')}
      </Button>
      <p className="text-xs text-brand-slate-500">{t('acknowledge.disclaimer')}</p>
    </div>
  );
}
