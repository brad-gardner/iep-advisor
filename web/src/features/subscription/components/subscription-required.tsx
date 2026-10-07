import { useTranslation } from 'react-i18next';
import { Lock } from 'lucide-react';
import { Link } from 'react-router-dom';
import { EmptyState } from '@/components/ui/empty-state';
import { SubscribeButton } from './subscribe-button';

export function SubscriptionRequired() {
  const { t } = useTranslation('subscription');
  return (
    <EmptyState
      icon={Lock}
      title={t('required.title')}
      description={t('required.description')}
      action={
        <div className="flex flex-col items-center gap-4">
          <SubscribeButton />
          <Link
            to="/redeem-invite"
            className="text-sm text-brand-teal-500 hover:text-brand-teal-600 underline"
          >
            {t('page.haveInviteCode')}
          </Link>
        </div>
      }
    />
  );
}
