import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { SubscribeButton } from '@/features/subscription/components/subscribe-button';
import { isUsageCapped } from '../lib/usage';
import { USAGE_WARNING_RATIO, type AdvocateUsageDto } from '../types/advocate';

interface UsageNoticeProps {
  usage: AdvocateUsageDto | null;
}

/**
 * Nothing below 80 % of the year's allowance; a heads-up from 80 %; at 100 %
 * the same subscription call-to-action the analysis limit uses (subscribe +
 * invite code) — or, for a subscriber who has spent the larger allowance, a
 * plain statement with a link to the subscription page.
 */
export function UsageNotice({ usage }: UsageNoticeProps) {
  const { t } = useTranslation('advocate');
  if (!usage || usage.limit <= 0) return null;
  const capped = isUsageCapped(usage);
  if (!capped && usage.used / usage.limit < USAGE_WARNING_RATIO) return null;

  if (!capped) {
    return (
      <Notice
        variant="warning"
        title={t('usage.warningTitle', { used: usage.used, limit: usage.limit })}
        data-testid="advocate-usage-warning"
      >
        {usage.subscriptionActive ? (
          <span>{t('usage.resetsNextYear')}</span>
        ) : (
          <span>
            {t('usage.subscribingRaises')}{' '}
            <Link to="/subscription" className="underline text-brand-teal-500 hover:text-brand-teal-600">
              {t('usage.seePlans')}
            </Link>
          </span>
        )}
      </Notice>
    );
  }

  return (
    <div role="status" data-testid="advocate-usage-capped">
      <Notice variant="warning" title={t('usage.cappedTitle', { limit: usage.limit })}>
        {usage.subscriptionActive ? (
          <span>
            {t('usage.resetsNextYear')}{' '}
            <Link to="/subscription" className="underline text-brand-teal-500 hover:text-brand-teal-600">
              {t('usage.manageSubscription')}
            </Link>
          </span>
        ) : (
          <div className="mt-2 flex flex-col items-start gap-3">
            <span>{t('usage.subscribeCta')}</span>
            <SubscribeButton />
            <Link to="/redeem-invite" className="text-sm text-brand-teal-500 hover:text-brand-teal-600 underline">
              {t('usage.inviteCode')}
            </Link>
          </div>
        )}
      </Notice>
    </div>
  );
}
