import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { ChangeSummaryChips } from '@/features/shared-drafts/components/change-summary-chips';
import { formatDate } from '@/lib/format-date';
import { useDraftShares } from '../hooks/use-draft-shares';

interface SharedBannerProps {
  instanceId: number;
}

/** "Shared as revision N on {date}" plus "Changes since last share" — renders
 *  nothing until there is an Active shared revision to summarize. */
export function SharedBanner({ instanceId }: SharedBannerProps) {
  // `shared-drafts` reached for `ChangeSummaryChips`'s own `t()` calls — see
  // the README's "reach another namespace" gotcha.
  const { t } = useTranslation(['draft-sharing', 'shared-drafts']);
  const { latestActive, isLoading, error } = useDraftShares(instanceId);

  if (isLoading || error || !latestActive) return null;

  return (
    <Notice
      variant="info"
      title={t('sharedBanner.titleLine', { number: latestActive.revisionNumber, date: formatDate(latestActive.sharedAt) })}
      data-testid="shared-banner"
    >
      <div className="space-y-2">
        <p>
          {latestActive.openResponseCount > 0
            ? t('sharedBanner.openResponses', { count: latestActive.openResponseCount })
            : t('sharedBanner.noOpenResponses')}
        </p>
        {latestActive.changeSummary && (
          <div>
            <p className="text-xs font-medium text-brand-slate-500">{t('converge.changesSinceLastShare')}</p>
            <ChangeSummaryChips summary={latestActive.changeSummary} data-testid="shared-banner-changes" />
          </div>
        )}
      </div>
    </Notice>
  );
}
