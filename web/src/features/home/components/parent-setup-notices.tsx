import { Notice } from '@/components/ui/notice';
import type { User } from '@/types/api';
import { AccountSetupNotices } from './account-setup-notices';
import { hasAccountSetupNotices } from '../lib/account-setup';

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
  const hasAny = hasAccountSetupNotices(user) || notices.length > 0;
  if (!hasAny) return null;

  return (
    <div className="space-y-3" data-testid="parent-home-setup-notices">
      <AccountSetupNotices user={user} />

      {notices.map((notice, i) => (
        <Notice key={`${notice}-${i}`} variant="info" title={notice} data-testid="parent-home-setup-notice" />
      ))}
    </div>
  );
}
