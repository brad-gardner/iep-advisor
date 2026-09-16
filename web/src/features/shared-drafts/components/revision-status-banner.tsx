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
  if (status === 'Active') return null;

  if (status === 'Withdrawn') {
    return (
      <Notice variant="warning" title="This draft was withdrawn" data-testid="revision-withdrawn-banner">
        The school withdrew this shared draft{withdrawnAt ? ` on ${formatDate(withdrawnAt)}` : ''}. It's
        kept here for your records, but it no longer reflects the current plan.
      </Notice>
    );
  }

  return (
    <Notice variant="info" title="This is a past revision" data-testid="revision-superseded-banner">
      A newer revision has since been shared. Use the revision list below to see the latest.
    </Notice>
  );
}
