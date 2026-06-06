import { useEducatorProfile } from '../hooks/use-educator-profile';
import { EducatorOnboardingForm } from '../components/educator-onboarding-form';
import { EducatorDashboard } from '../components/educator-dashboard';

export function EducatorHomePage() {
  const { profile, isOnboarded, isLoading } = useEducatorProfile();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Educator</h1>
      {isOnboarded && profile ? (
        <EducatorDashboard profile={profile} />
      ) : (
        <EducatorOnboardingForm />
      )}
    </div>
  );
}
