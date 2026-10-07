import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PageLayout } from '@/components/ui/page-layout';
import { usePageTitle } from '@/hooks/use-page-title';
import { SubscriptionStatusCard } from './subscription-status';

export function SubscriptionPage() {
  const { t } = useTranslation('subscription');
  usePageTitle(t('page.pageTitle'));
  return (
    <PageLayout title={t('page.pageTitle')}>
      <div className="max-w-lg">
        <SubscriptionStatusCard />
      </div>

      <p className="text-sm text-brand-slate-500">
        {t('page.haveInviteCode')}{' '}
        <Link to="/redeem-invite" className="text-brand-teal-500 hover:text-brand-teal-600 underline">
          {t('page.redeemHere')}
        </Link>
      </p>
    </PageLayout>
  );
}
