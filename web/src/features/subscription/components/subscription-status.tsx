import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { SubscribeButton } from './subscribe-button';
import { createPortalSession } from '../api/subscription-api';
import { useSubscription } from '../hooks/use-subscription';
import type { SubscriptionStatus as SubscriptionStatusType, ChildUsage } from '@/types/api';

function statusBadge(status: string) {
  switch (status) {
    case 'active':
      return <Badge variant="success">Active</Badge>;
    case 'past_due':
      return <Badge variant="warning">Past Due</Badge>;
    case 'canceled':
      return <Badge variant="neutral">Canceled</Badge>;
    case 'expired':
      return <Badge variant="neutral">Expired</Badge>;
    default:
      return <Badge variant="neutral">No Subscription</Badge>;
  }
}

function UsageBar({ usage }: { usage: ChildUsage }) {
  const pct = usage.analysisLimit > 0
    ? Math.min((usage.analysisCount / usage.analysisLimit) * 100, 100)
    : 0;

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span className="text-brand-slate-600 font-medium">{usage.childName}</span>
        <span className="text-brand-slate-400 text-xs">
          {usage.analysisCount} of {usage.analysisLimit} analyses used
        </span>
      </div>
      <div className="h-2 bg-brand-slate-100 rounded-full overflow-hidden">
        <div
          className="h-full bg-brand-teal-500 rounded-full transition-all"
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

function ManageButton() {
  const [isLoading, setIsLoading] = useState(false);

  const handleManage = async () => {
    setIsLoading(true);
    try {
      const { url } = await createPortalSession(window.location.href);
      window.location.href = url;
    } catch {
      setIsLoading(false);
    }
  };

  return (
    <Button variant="secondary" onClick={handleManage} disabled={isLoading}>
      {isLoading ? 'Redirecting...' : 'Manage Subscription'}
    </Button>
  );
}

function StatusContent({ data }: { data: SubscriptionStatusType }) {
  const isActive = data.status === 'active';
  const usageEntries = data.childUsage ? Object.values(data.childUsage) : [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-serif text-lg font-semibold text-brand-slate-800">
          Subscription
        </h3>
        {statusBadge(data.status)}
      </div>

      {isActive && data.expiresAt && (
        <p className="text-sm text-brand-slate-400">
          Renews{' '}
          {new Date(data.expiresAt).toLocaleDateString('en-US', {
            month: 'long',
            day: 'numeric',
            year: 'numeric',
          })}
        </p>
      )}

      {usageEntries.length > 0 && (
        <div className="space-y-3">
          <p className="text-xs font-semibold text-brand-slate-400 uppercase tracking-wide">
            Usage
          </p>
          {usageEntries.map((u) => (
            <UsageBar key={u.childId} usage={u} />
          ))}
        </div>
      )}

      <div className="pt-2">
        {isActive || data.status === 'past_due' ? (
          <ManageButton />
        ) : (
          <SubscribeButton />
        )}
      </div>
    </div>
  );
}

export function SubscriptionStatusCard() {
  const { status, isLoading } = useSubscription();

  if (isLoading) {
    return (
      <Card>
        <div className="animate-pulse space-y-3">
          <div className="h-5 bg-brand-slate-100 rounded w-1/3" />
          <div className="h-3 bg-brand-slate-100 rounded w-1/2" />
        </div>
      </Card>
    );
  }

  if (!status) {
    return (
      <Card>
        <div className="space-y-4">
          <h3 className="font-serif text-lg font-semibold text-brand-slate-800">
            Subscription
          </h3>
          <p className="text-sm text-brand-slate-400">
            Unable to load subscription status.
          </p>
        </div>
      </Card>
    );
  }

  return (
    <Card>
      <StatusContent data={status} />
    </Card>
  );
}
