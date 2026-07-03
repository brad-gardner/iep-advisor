import { Link } from 'react-router-dom';
import { XCircle } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { PageLayout } from '@/components/ui/page-layout';

export function SubscriptionCancelPage() {
  return (
    <PageLayout title="Subscription">
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
    </PageLayout>
  );
}
