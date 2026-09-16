import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import type { User } from '@/types/api';

interface ParentSetupNoticesProps {
  user: User | null;
  /** Server-computed notices, e.g. "No school link yet" for an individual child. */
  notices: string[];
}

/**
 * The same account-setup notices `LegacyParentHome` shows at the top, demoted
 * to the bottom for a parent who already has the new operational sections
 * above — plus any server-computed per-child setup notices.
 */
export function ParentSetupNotices({ user, notices }: ParentSetupNoticesProps) {
  const hasAny = (user && !user.onboardingCompleted) || (!user?.state && user?.onboardingCompleted) || notices.length > 0;
  if (!hasAny) return null;

  return (
    <div className="space-y-3" data-testid="parent-home-setup-notices">
      {user && !user.onboardingCompleted && (
        <div data-testid="onboarding-banner">
          <Notice variant="info" title="Complete your setup to get the most out of IEP Advisor">
            <Link to="/onboarding">
              <Button variant="primary" className="mt-2 gap-1.5" data-testid="onboarding-get-started">
                Get Started
                <ArrowRight size={14} strokeWidth={1.8} aria-hidden="true" />
              </Button>
            </Link>
          </Notice>
        </div>
      )}

      {!user?.state && user?.onboardingCompleted && (
        <Notice variant="warning" title="Set your state for better guidance">
          <Link to="/profile" className="underline hover:text-brand-amber-600">
            Update your profile
          </Link>{' '}
          to get jurisdiction-specific IEP guidance.
        </Notice>
      )}

      {notices.map((notice, i) => (
        <Notice key={`${notice}-${i}`} variant="info" title={notice} data-testid="parent-home-setup-notice" />
      ))}
    </div>
  );
}
