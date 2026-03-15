import { Link } from 'react-router-dom';
import { CheckCircle } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export function SubscriptionSuccessPage() {
  return (
    <div className="space-y-6">
      <h1 className="font-serif">Subscription</h1>

      <Card className="max-w-md">
        <div className="flex flex-col items-center text-center py-6">
          <div className="w-12 h-12 rounded-full bg-brand-teal-50 flex items-center justify-center mb-4">
            <CheckCircle className="w-6 h-6 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          </div>
          <h2 className="font-serif text-xl font-semibold text-brand-slate-800 mb-2">
            You're subscribed!
          </h2>
          <p className="text-sm text-brand-slate-400 mb-6">
            Thank you for subscribing to IEP Advisor Pro. You now have full access to all features.
          </p>
          <Link to="/dashboard">
            <Button>Go to Dashboard</Button>
          </Link>
        </div>
      </Card>
    </div>
  );
}
