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
  const { latestActive, isLoading, error } = useDraftShares(instanceId);

  if (isLoading || error || !latestActive) return null;

  return (
    <Notice
      variant="info"
      title={`Shared as revision ${latestActive.revisionNumber} on ${formatDate(latestActive.sharedAt)}`}
      data-testid="shared-banner"
    >
      <div className="space-y-2">
        <p>
          {latestActive.openResponseCount > 0
            ? `${latestActive.openResponseCount} open ${latestActive.openResponseCount === 1 ? 'response' : 'responses'} from the family.`
            : 'No open responses yet.'}
        </p>
        {latestActive.changeSummary && (
          <div>
            <p className="text-xs font-medium text-brand-slate-500">Changes since last share</p>
            <ChangeSummaryChips summary={latestActive.changeSummary} data-testid="shared-banner-changes" />
          </div>
        )}
      </div>
    </Notice>
  );
}
