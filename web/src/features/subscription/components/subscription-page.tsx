import { Link } from 'react-router-dom';
import { PageLayout } from '@/components/ui/page-layout';
import { SubscriptionStatusCard } from './subscription-status';

export function SubscriptionPage() {
  return (
    <PageLayout title="Subscription">
      <div className="max-w-lg">
        <SubscriptionStatusCard />
      </div>

      <p className="text-sm text-brand-slate-400">
        Have an invite code?{' '}
        <Link to="/redeem-invite" className="text-brand-teal-500 hover:text-brand-teal-600 underline">
          Redeem it here
        </Link>
      </p>
    </PageLayout>
  );
}
