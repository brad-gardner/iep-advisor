import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Logo } from '@/components/ui/logo';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { WelcomeStep } from './welcome-step';
import { StateStep } from './state-step';
import { ChildStep } from './child-step';
import { NextSteps } from './next-steps';

const TOTAL_STEPS = 4;

function ProgressDots({ current, total }: { current: number; total: number }) {
  return (
    <div className="flex items-center justify-center gap-2">
      {Array.from({ length: total }, (_, i) => (
        <div
          key={i}
          className={`w-2 h-2 rounded-full transition-colors ${
            i <= current ? 'bg-brand-teal-500' : 'bg-brand-slate-200'
          }`}
        />
      ))}
    </div>
  );
}

export function OnboardingFlow() {
  const [step, setStep] = useState(0);
  const navigate = useNavigate();
  const { completeOnboarding } = useAuth();

  const next = () => setStep((s) => Math.min(s + 1, TOTAL_STEPS - 1));

  const handleFinish = async () => {
    await completeOnboarding();
    navigate('/dashboard');
  };

  return (
    <div className="min-h-screen bg-brand-slate-50 flex flex-col">
      {/* Header */}
      <div className="flex justify-center pt-8 pb-4">
        <Logo variant="light" size="md" />
      </div>

      {/* Progress */}
      <div className="flex flex-col items-center gap-1.5 pb-6">
        <ProgressDots current={step} total={TOTAL_STEPS} />
        <p className="text-xs text-brand-slate-400">
          Step {step + 1} of {TOTAL_STEPS}
        </p>
      </div>

      {/* Content */}
      <div className="flex-1 flex items-start justify-center px-4 pb-12">
        <div className="bg-white rounded-card border-[0.5px] border-brand-slate-200 p-8 w-full max-w-xl">
          {step === 0 && <WelcomeStep onNext={next} />}
          {step === 1 && <StateStep onNext={next} onSkip={next} />}
          {step === 2 && <ChildStep onNext={next} onSkip={next} />}
          {step === 3 && <NextSteps onFinish={handleFinish} />}
        </div>
      </div>
    </div>
  );
}
