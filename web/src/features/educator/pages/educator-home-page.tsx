import { Link } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { PageLayout } from '@/components/ui/page-layout';
import { orgRoleLabel } from '@/lib/org-role-label';
import { useEducatorProfile } from '../hooks/use-educator-profile';
import { EducatorDashboard } from '../components/educator-dashboard';
import { DeactivatedAccessNotice } from '../components/deactivated-access-notice';

export function EducatorHomePage() {
  const { profile, isLoading } = useEducatorProfile();

  // While the async profile (which decides the whole home) resolves, show a
  // header + body skeleton rather than a wrong-home flash. Skeletons are
  // decorative (aria-hidden), so a sibling status region announces the load.
  if (isLoading) {
    return (
      <div className="space-y-6" role="status" aria-label="Loading your home">
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-40" />
        </div>
        <Skeleton className="h-44 w-full max-w-lg" />
        <span className="sr-only">Loading…</span>
      </div>
    );
  }

  // Staff always have a profile now (created by district signup or an invite).
  // A null profile should not happen for real staff; it can only occur if a
  // platform Admin flipped a user to Educator without provisioning a profile —
  // show a clear support notice rather than any onboarding flow.
  if (profile == null) {
    return (
      <PageLayout title="Home">
        <div data-testid="educator-no-profile">
          <Notice variant="warning" title="No staff profile found">
            Your account is not linked to a school or district. Please contact
            support to finish setting up your access.
          </Notice>
        </div>
      </PageLayout>
    );
  }

  if (!profile.isActive) {
    return (
      <PageLayout title="Home">
        <DeactivatedAccessNotice />
      </PageLayout>
    );
  }

  // Active profile: identity moves into the page header (title = the school or
  // district name; subtitle = human role + state), with "View students" as the
  // header action. The body is operational modules only.
  const subtitle = [orgRoleLabel(profile.orgRoleName), profile.stateCode]
    .filter(Boolean)
    .join(' · ');

  return (
    <PageLayout
      title={profile.schoolName || profile.districtName}
      subtitle={subtitle || undefined}
      actions={
        <Link to="/educator/students">
          <Button variant="secondary" data-testid="educator-students-link">
            View students
          </Button>
        </Link>
      }
    >
      <EducatorDashboard profile={profile} />
    </PageLayout>
  );
}
