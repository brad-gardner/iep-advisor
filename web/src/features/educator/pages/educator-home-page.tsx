import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { PageLayout } from '@/components/ui/page-layout';
import { useEducatorProfile } from '../hooks/use-educator-profile';
import { EducatorDashboard } from '../components/educator-dashboard';
import { DeactivatedAccessNotice } from '../components/deactivated-access-notice';

export function EducatorHomePage() {
  const { profile, isLoading } = useEducatorProfile();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner />
      </div>
    );
  }

  // Staff always have a profile now (created by district signup or an invite).
  // A null profile should not happen for real staff; it can only occur if a
  // platform Admin flipped a user to Educator without provisioning a profile —
  // show a clear support notice rather than any onboarding flow.
  const isDeactivated = profile != null && !profile.isActive;

  return (
    <PageLayout title="Educator">
      {profile == null ? (
        <div data-testid="educator-no-profile">
          <Notice variant="warning" title="No staff profile found">
            Your account is not linked to a school or district. Please contact
            support to finish setting up your access.
          </Notice>
        </div>
      ) : isDeactivated ? (
        <DeactivatedAccessNotice />
      ) : (
        <EducatorDashboard profile={profile} />
      )}
    </PageLayout>
  );
}
