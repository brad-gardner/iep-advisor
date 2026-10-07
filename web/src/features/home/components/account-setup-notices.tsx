import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import type { User } from '@/types/api';

interface AccountSetupNoticesProps {
  user: User | null;
}

/**
 * The account-setup notices shared by both parent-home render paths:
 * "complete your setup" (onboarding) and "set your state" (jurisdiction
 * guidance). `LegacyParentHome` renders this at the top; `ParentSetupNotices`
 * renders the same block demoted to the bottom, plus any server-computed
 * per-child notices.
 */
export function AccountSetupNotices({ user }: AccountSetupNoticesProps) {
  const { t } = useTranslation('home');
  return (
    <>
      {user && !user.onboardingCompleted && (
        <div data-testid="onboarding-banner">
          <Notice variant="info" title={t('accountSetupNotices.completeSetupTitle')}>
            <Link to="/onboarding">
              <Button variant="primary" className="mt-2 gap-1.5" data-testid="onboarding-get-started">
                {t('accountSetupNotices.getStarted')}
                <ArrowRight size={14} strokeWidth={1.8} aria-hidden="true" />
              </Button>
            </Link>
          </Notice>
        </div>
      )}

      {!user?.state && user?.onboardingCompleted && (
        <Notice variant="warning" title={t('accountSetupNotices.setStateTitle')}>
          <Link to="/profile" className="underline hover:text-brand-amber-600">
            {t('accountSetupNotices.updateProfile')}
          </Link>{' '}
          {t('accountSetupNotices.setStateSuffix')}
        </Notice>
      )}
    </>
  );
}
