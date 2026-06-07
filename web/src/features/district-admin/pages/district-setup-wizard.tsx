import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Logo } from '@/components/ui/logo';
import { ProgressDots } from '@/components/ui/progress-dots';
import { ORG_ROLE } from '@/features/educator/types';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { SetupWelcomeStep } from '../components/setup/setup-welcome-step';
import { SetupSchoolStep } from '../components/setup/setup-school-step';
import { SetupStaffStep } from '../components/setup/setup-staff-step';
import { SetupDoneStep } from '../components/setup/setup-done-step';
import type { DistrictSchool } from '../types';

const STEP_LABELS = ['Welcome', 'Create school', 'Invite staff', 'Done'];
const TOTAL_STEPS = STEP_LABELS.length;
const DONE_STEP = TOTAL_STEPS - 1;

// First-run wizard for a new DistrictAdmin. Only meaningful for DistrictAdmins;
// other staff are redirected to the dashboard. Every step is skippable — the
// dashboard checklist nudges anything left undone.
export function DistrictSetupWizard() {
  const { profile, isLoading } = useEducatorProfile();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [createdSchool, setCreatedSchool] = useState<DistrictSchool | null>(null);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  // The wizard only makes sense for a DistrictAdmin who can create schools and
  // invite staff. Anyone else (including a missing/non-admin profile) goes to
  // the regular educator home.
  if (!profile || profile.orgRoleId !== ORG_ROLE.DistrictAdmin) {
    return <Navigate to="/educator" replace />;
  }

  const next = () => setStep((s) => Math.min(s + 1, DONE_STEP));
  const finish = () => navigate('/educator');

  return (
    <div className="min-h-screen bg-brand-slate-50 flex flex-col">
      <div className="flex justify-center pt-8 pb-4">
        <Logo variant="light" size="md" />
      </div>

      <div className="flex flex-col items-center gap-1.5 pb-6">
        <ProgressDots
          current={step}
          total={TOTAL_STEPS}
          labels={STEP_LABELS}
          testId="district-setup-progress"
        />
        <p className="text-xs text-brand-slate-400" aria-live="polite">
          Step {step + 1} of {TOTAL_STEPS}
        </p>
      </div>

      <div className="flex-1 flex items-start justify-center px-4 pb-12">
        <div
          className="bg-white rounded-card border-[0.5px] border-brand-slate-200 p-8 w-full max-w-xl"
          data-testid="district-setup-step"
        >
          {step === 0 && (
            <SetupWelcomeStep
              districtName={profile.districtName}
              onNext={next}
              onSkip={finish}
            />
          )}
          {step === 1 && (
            <SetupSchoolStep
              createdSchool={createdSchool}
              onCreated={setCreatedSchool}
              onNext={next}
              onSkip={next}
            />
          )}
          {step === 2 && (
            <SetupStaffStep
              schools={createdSchool ? [createdSchool] : []}
              onNext={next}
              onSkip={next}
            />
          )}
          {step === 3 && (
            <SetupDoneStep createdSchool={createdSchool} onFinish={finish} />
          )}
        </div>
      </div>
    </div>
  );
}
