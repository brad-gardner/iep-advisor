import { Button } from '@/components/ui/button';

interface SetupWelcomeStepProps {
  districtName: string;
  onNext: () => void;
  onSkip: () => void;
}

// Step 1: orients a new DistrictAdmin to what the wizard will do. Skippable —
// nothing is created here.
export function SetupWelcomeStep({ districtName, onNext, onSkip }: SetupWelcomeStepProps) {
  return (
    <div className="space-y-6" data-testid="district-setup-welcome">
      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">
          Welcome{districtName ? `, ${districtName}` : ''}
        </h2>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          Let's get your district set up. In the next couple of steps you'll
          create your first school and invite a staff member. You can skip any
          step and finish later from your dashboard.
        </p>
      </div>

      <div className="flex gap-2">
        <Button onClick={onNext} data-testid="district-setup-next-0">
          Start setup
        </Button>
        <Button variant="ghost" onClick={onSkip} data-testid="district-setup-skip-0">
          Skip for now
        </Button>
      </div>
    </div>
  );
}
