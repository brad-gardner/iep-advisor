import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { onboardEducator } from '../api/educator-api';

export function EducatorOnboardingForm() {
  const navigate = useNavigate();
  const { refreshUser } = useAuth();
  const [districtName, setDistrictName] = useState('');
  const [schoolName, setSchoolName] = useState('');
  const [stateCode, setStateCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      const response = await onboardEducator({
        districtName: districtName.trim(),
        schoolName: schoolName.trim(),
        stateCode: stateCode.trim() || undefined,
      });

      if (response.success) {
        // Onboarding flips the server-side role Parent -> Educator; refetch the
        // user so the client (sidebar, routing) reflects the new role.
        await refreshUser();
        navigate('/educator/students');
        return;
      }

      setError(response.message || 'Onboarding failed');
    } catch {
      setError('An error occurred during onboarding');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-xl mb-1">Set up your school</h2>
      <p className="text-sm text-brand-slate-500 mb-4">
        Tell us where you teach so you can manage students and invite parents.
      </p>
      <form onSubmit={handleSubmit} className="space-y-4" data-testid="educator-onboarding-form">
        {error && <Notice variant="error" title={error} />}

        <Input
          label="District Name *"
          required
          value={districtName}
          onChange={(e) => setDistrictName(e.target.value)}
          maxLength={200}
          data-testid="educator-district-name"
        />

        <Input
          label="School Name *"
          required
          value={schoolName}
          onChange={(e) => setSchoolName(e.target.value)}
          maxLength={200}
          data-testid="educator-school-name"
        />

        <Input
          label="State Code"
          placeholder="e.g. OH"
          value={stateCode}
          onChange={(e) => setStateCode(e.target.value.toUpperCase())}
          maxLength={2}
          data-testid="educator-state-code"
        />

        <Button
          type="submit"
          disabled={isSubmitting}
          className="w-full"
          data-testid="educator-onboarding-submit"
        >
          {isSubmitting ? 'Saving...' : 'Continue'}
        </Button>
      </form>
    </Card>
  );
}
