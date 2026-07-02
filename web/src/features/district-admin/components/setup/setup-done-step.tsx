import { CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { DistrictSchool } from '../../types';

interface SetupDoneStepProps {
  createdSchool: DistrictSchool | null;
  onFinish: () => void;
}

// Step 4: recap what was set up and hand off to the dashboard.
export function SetupDoneStep({ createdSchool, onFinish }: SetupDoneStepProps) {
  return (
    <div className="space-y-6 text-center" data-testid="district-setup-done">
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

      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">You're all set</h2>
        <p className="text-sm text-brand-slate-500 max-w-md mx-auto leading-relaxed">
          {createdSchool
            ? `${createdSchool.name} is ready. You can manage schools, invite staff, and add students any time from your dashboard.`
            : 'You can create schools, invite staff, and add students any time from your dashboard.'}
        </p>
      </div>

      <Button onClick={onFinish} className="mt-2" data-testid="district-setup-finish">
        Go to dashboard
      </Button>
    </div>
  );
}
