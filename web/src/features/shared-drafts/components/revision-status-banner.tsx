import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { formatDate } from '@/lib/format-date';
import type { SharedDraftStatus } from '../types';

interface RevisionStatusBannerProps {
  status: SharedDraftStatus;
  withdrawnAt: string | null;
}

/** Explains a non-Active revision. Active renders nothing — the page is the
 *  ordinary reading view. Not a load error, so no `role="alert"`. */
export function RevisionStatusBanner({ status, withdrawnAt }: RevisionStatusBannerProps) {
  const { t } = useTranslation('shared-drafts');
  if (status === 'Active') return null;

  if (status === 'Withdrawn') {
    return (
      <Notice variant="warning" title={t('statusBanner.withdrawnTitle')} data-testid="revision-withdrawn-banner">
        {withdrawnAt
          ? t('statusBanner.withdrawnBodyDated', { date: formatDate(withdrawnAt) })
          : t('statusBanner.withdrawnBody')}
      </Notice>
    );
  }

  return (
    <Notice variant="info" title={t('statusBanner.supersededTitle')} data-testid="revision-superseded-banner">
      {t('statusBanner.supersededBody')}
    </Notice>
  );
}
