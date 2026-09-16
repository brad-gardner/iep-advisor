import type { User } from '@/types/api';

/** Whether `AccountSetupNotices` has anything to show for this user. */
export function hasAccountSetupNotices(user: User | null): boolean {
  return Boolean((user && !user.onboardingCompleted) || (!user?.state && user?.onboardingCompleted));
}
