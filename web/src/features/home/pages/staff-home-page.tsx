import { Link } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { PageLayout } from '@/components/ui/page-layout';
import { orgRoleLabel } from '@/lib/org-role-label';
import { usePageTitle } from '@/hooks/use-page-title';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { DeactivatedAccessNotice } from '@/features/educator/components/deactivated-access-notice';
import { StaffHomeBody } from '../components/staff-home-body';

/**
 * The staff (Educator role) home shell — variant dispatch happens one level
 * down in `StaffHomeBody`/`CaseloadHome`/`AdminHome` once the single `/api/home`
 * fetch resolves. This page only owns the profile-loading/deactivated/no-profile
 * guards (unchanged from the previous `EducatorHomePage`) plus the page chrome.
 */
export function StaffHomePage() {
  const { profile, isLoading } = useEducatorProfile();
  // Title upgrades from a generic "Home" to the org name once the profile
  // resolves — called unconditionally so every guard state below gets a title.
  usePageTitle(profile ? profile.schoolName || profile.districtName : 'Home');

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
      <StaffHomeBody />
    </PageLayout>
  );
}
