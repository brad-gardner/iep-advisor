import { CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface WelcomeStepProps {
  onNext: () => void;
}

export function WelcomeStep({ onNext }: WelcomeStepProps) {
  return (
    <div className="text-center space-y-6">
      <div className="flex justify-center">
        <div className="bg-brand-teal-50 rounded-full p-4">
          <CheckCircle
            className="text-brand-teal-500"
            size={48}
            strokeWidth={1.8}
            aria-hidden="true"
          />
        </div>
      </div>

      <div className="space-y-3">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          Welcome to IEP Advisor
        </h1>
        <p className="text-sm text-brand-slate-500 max-w-md mx-auto leading-relaxed">
          You're taking an important step for your child. IEP Advisor helps you
          understand your child's Individualized Education Program, know your
          rights, and walk into every meeting prepared and confident.
        </p>
      </div>

      <Button onClick={onNext} className="mt-4" data-testid="onboarding-start">
        Let's get you set up
      </Button>
    </div>
  );
}
