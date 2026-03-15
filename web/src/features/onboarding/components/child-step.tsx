import { Button } from '@/components/ui/button';
import { ChildForm } from '@/features/children/components/child-form';
import { createChild } from '@/features/children/api/children-api';
import type { CreateChildProfileRequest } from '@/types/api';

interface ChildStepProps {
  onNext: () => void;
  onSkip: () => void;
}

export function ChildStep({ onNext, onSkip }: ChildStepProps) {
  const handleSubmit = async (
    data: CreateChildProfileRequest
  ): Promise<{ success: boolean; error?: string }> => {
    try {
      const response = await createChild(data);
      if (response.success) {
        onNext();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to create child profile' };
    } catch {
      return { success: false, error: 'An error occurred creating the profile' };
    }
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          Add Your First Child
        </h1>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          We'll use this information to personalize your IEP analysis. Only a
          first name is required — you can add more details later.
        </p>
      </div>

      <ChildForm onSubmit={handleSubmit} submitLabel="Save & Continue" />

      <div className="flex justify-start">
        <Button variant="ghost" onClick={onSkip}>
          Skip for now
        </Button>
      </div>
    </div>
  );
}
