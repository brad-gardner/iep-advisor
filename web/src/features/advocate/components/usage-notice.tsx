import { Link } from 'react-router-dom';
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
  if (!usage || usage.limit <= 0) return null;
  const capped = isUsageCapped(usage);
  if (!capped && usage.used / usage.limit < USAGE_WARNING_RATIO) return null;

  if (!capped) {
    return (
      <Notice
        variant="warning"
        title={`You've used ${usage.used} of ${usage.limit} advocate messages this year`}
        data-testid="advocate-usage-warning"
      >
        {usage.subscriptionActive ? (
          <span>The allowance resets at the start of next year.</span>
        ) : (
          <span>
            Subscribing raises the yearly allowance.{' '}
            <Link to="/subscription" className="underline text-brand-teal-500 hover:text-brand-teal-600">
              See plans
            </Link>
          </span>
        )}
      </Notice>
    );
  }

  return (
    <div role="status" data-testid="advocate-usage-capped">
      <Notice variant="warning" title={`You've used all ${usage.limit} advocate messages for this year`}>
        {usage.subscriptionActive ? (
          <span>
            The allowance resets at the start of next year.{' '}
            <Link to="/subscription" className="underline text-brand-teal-500 hover:text-brand-teal-600">
              Manage subscription
            </Link>
          </span>
        ) : (
          <div className="mt-2 flex flex-col items-start gap-3">
            <span>Subscribe to IEP Advisor Pro to keep the conversation going.</span>
            <SubscribeButton />
            <Link to="/redeem-invite" className="text-sm text-brand-teal-500 hover:text-brand-teal-600 underline">
              Have an invite code?
            </Link>
          </div>
        )}
      </Notice>
    </div>
  );
}
