import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { SubscribeButton } from './subscribe-button';
import { createPortalSession } from '../api/subscription-api';
import { useSubscription } from '../hooks/use-subscription';
import { getActiveLanguage } from '@/lib/i18n/format';
import i18n from '@/lib/i18n';
import type { SubscriptionStatus as SubscriptionStatusType, ChildUsage } from '@/types/api';

function statusBadge(status: string) {
  switch (status) {
    case 'active':
      return <Badge variant="success" data-testid="subscription-status">{i18n.t('subscription:status.active')}</Badge>;
    case 'past_due':
      return <Badge variant="warning" data-testid="subscription-status">{i18n.t('subscription:status.pastDue')}</Badge>;
    case 'canceled':
      return <Badge variant="neutral" data-testid="subscription-status">{i18n.t('subscription:status.canceled')}</Badge>;
    case 'expired':
      return <Badge variant="neutral" data-testid="subscription-status">{i18n.t('subscription:status.expired')}</Badge>;
    default:
      return <Badge variant="neutral" data-testid="subscription-status">{i18n.t('subscription:status.none')}</Badge>;
  }
}

function UsageBar({ usage }: { usage: ChildUsage }) {
  const { t } = useTranslation('subscription');
  const pct = usage.analysisLimit > 0
    ? Math.min((usage.analysisCount / usage.analysisLimit) * 100, 100)
    : 0;

  return (
    <div className="space-y-1" data-testid="usage-bar">
      <div className="flex items-center justify-between text-sm">
        <span className="text-brand-slate-600 font-medium">{usage.childName}</span>
        <span className="text-brand-slate-500 text-xs">
          {t('status.analysesUsed', { count: usage.analysisCount, limit: usage.analysisLimit })}
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
  const { t } = useTranslation('subscription');
  const { show } = useToast();
  const [isLoading, setIsLoading] = useState(false);

  const handleManage = async () => {
    setIsLoading(true);
    try {
      const { url } = await createPortalSession(window.location.href);
      window.location.href = url;
    } catch {
      // Redirect failed — surface it instead of silently resetting.
      show({ message: t('status.manageFailed'), variant: 'error' });
      setIsLoading(false);
    }
  };

  return (
    <Button variant="secondary" onClick={handleManage} loading={isLoading}>
      {t('status.manage')}
    </Button>
  );
}

function StatusContent({ data }: { data: SubscriptionStatusType }) {
  const { t } = useTranslation('subscription');
  const isActive = data.status === 'active';
  const usageEntries = data.childUsage ? Object.values(data.childUsage) : [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-serif text-lg font-semibold text-brand-slate-800">
          {t('status.yourPlan')}
        </h3>
        {statusBadge(data.status)}
      </div>

      {isActive && data.expiresAt && (
        <p className="text-sm text-brand-slate-500">
          {t('status.renews', {
            date: new Date(data.expiresAt).toLocaleDateString(getActiveLanguage(), {
              month: 'long',
              day: 'numeric',
              year: 'numeric',
            }),
          })}
        </p>
      )}

      {usageEntries.length > 0 && (
        <div className="space-y-3">
          <p className="text-xs font-semibold text-brand-slate-500 uppercase tracking-wide">
            {t('status.usage')}
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
  const { t } = useTranslation('subscription');
  const { status, isLoading, reload } = useSubscription();

  if (isLoading) {
    return (
      <Card>
        <div className="space-y-3">
          <Skeleton className="h-5 w-1/3" />
          <Skeleton className="h-3 w-1/2" />
        </div>
      </Card>
    );
  }

  if (!status) {
    return (
      <Card>
        <Notice variant="error" title={t('status.loadFailedTitle')}>
          <div className="mt-3">
            <Button variant="secondary" size="sm" onClick={() => reload()}>
              {t('common:ui.tryAgain')}
            </Button>
          </div>
        </Notice>
      </Card>
    );
  }

  return (
    <Card>
      <StatusContent data={status} />
    </Card>
  );
}
