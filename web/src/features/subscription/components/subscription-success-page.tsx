import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CheckCircle } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { PageLayout } from '@/components/ui/page-layout';
import { usePageTitle } from '@/hooks/use-page-title';

export function SubscriptionSuccessPage() {
  const { t } = useTranslation('subscription');
  usePageTitle(t('page.pageTitle'));
  return (
    <PageLayout title={t('page.pageTitle')}>
      <Card className="max-w-md">
        <div className="flex flex-col items-center text-center py-6">
          <div className="w-12 h-12 rounded-full bg-brand-teal-50 flex items-center justify-center mb-4">
            <CheckCircle className="w-6 h-6 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          </div>
          <h2 className="font-serif text-xl font-semibold text-brand-slate-800 mb-2">
            {t('successPage.heading')}
          </h2>
          <p className="text-sm text-brand-slate-500 mb-6">
            {t('successPage.body')}
          </p>
          <Link to="/dashboard">
            <Button>{t('successPage.goToDashboard')}</Button>
          </Link>
        </div>
      </Card>
    </PageLayout>
  );
}
