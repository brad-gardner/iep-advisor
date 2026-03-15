import { Link } from 'react-router-dom';
import { XCircle } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export function SubscriptionCancelPage() {
  return (
    <div className="space-y-6">
      <h1 className="font-serif">Subscription</h1>

      <Card className="max-w-md">
        <div className="flex flex-col items-center text-center py-6">
          <div className="w-12 h-12 rounded-full bg-brand-slate-100 flex items-center justify-center mb-4">
            <XCircle className="w-6 h-6 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />
          </div>
          <h2 className="font-serif text-xl font-semibold text-brand-slate-800 mb-2">
            Checkout Canceled
          </h2>
          <p className="text-sm text-brand-slate-400 mb-6">
            No worries — you can subscribe anytime.
          </p>
          <Link to="/subscription">
            <Button variant="secondary">Back to Subscription</Button>
          </Link>
        </div>
      </Card>
    </div>
  );
}
