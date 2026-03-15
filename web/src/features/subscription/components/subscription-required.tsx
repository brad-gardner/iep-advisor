import { Lock } from 'lucide-react';
import { Link } from 'react-router-dom';
import { SubscribeButton } from './subscribe-button';

export function SubscriptionRequired() {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="w-12 h-12 rounded-full bg-brand-slate-100 flex items-center justify-center mb-4">
        <Lock className="w-6 h-6 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />
      </div>
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        Subscription Required
      </h3>
      <p className="text-brand-slate-400 text-sm text-center max-w-md mb-6">
        Subscribe to IEP Advisor Pro to unlock this feature.
      </p>
      <SubscribeButton />
      <Link
        to="/redeem-invite"
        className="mt-4 text-sm text-brand-teal-500 hover:text-brand-teal-600 underline"
      >
        Have an invite code?
      </Link>
    </div>
  );
}
