import { Lock } from 'lucide-react';
import { Link } from 'react-router-dom';
import { EmptyState } from '@/components/ui/empty-state';
import { SubscribeButton } from './subscribe-button';

export function SubscriptionRequired() {
  return (
    <EmptyState
      icon={Lock}
      title="Subscription Required"
      description="Subscribe to IEP Advisor Pro to unlock this feature."
      action={
        <div className="flex flex-col items-center gap-4">
          <SubscribeButton />
          <Link
            to="/redeem-invite"
            className="text-sm text-brand-teal-500 hover:text-brand-teal-600 underline"
          >
            Have an invite code?
          </Link>
        </div>
      }
    />
  );
}
