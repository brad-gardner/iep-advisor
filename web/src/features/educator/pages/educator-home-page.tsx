import { useEducatorProfile } from '../hooks/use-educator-profile';
import { EducatorOnboardingForm } from '../components/educator-onboarding-form';
import { EducatorDashboard } from '../components/educator-dashboard';
import { DeactivatedAccessNotice } from '../components/deactivated-access-notice';

export function EducatorHomePage() {
  const { profile, isOnboarded, isLoading } = useEducatorProfile();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  // A deactivated staff member still has a profile (isActive=false). Show a
  // clear state instead of the dashboard.
  const isDeactivated = profile != null && !profile.isActive;

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Educator</h1>
      {isDeactivated ? (
        <DeactivatedAccessNotice />
      ) : isOnboarded && profile ? (
        <EducatorDashboard profile={profile} />
      ) : (
        <EducatorOnboardingForm />
      )}
    </div>
  );
}
