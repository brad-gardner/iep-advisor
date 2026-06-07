import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { reloadEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { createSchool } from '../../api/district-api';
import type { DistrictSchool, SaveSchoolRequest } from '../../types';
import { SchoolForm } from '../school-form';

interface SetupSchoolStepProps {
  // The school created in this session (if any), so a returning step shows the
  // confirmation rather than an empty form.
  createdSchool: DistrictSchool | null;
  onCreated: (school: DistrictSchool) => void;
  onNext: () => void;
  onSkip: () => void;
}

// Step 2: create the district's first school. Reuses the shared SchoolForm; on
// success the created school is lifted into wizard state so the staff step can
// target it. Skippable.
export function SetupSchoolStep({
  createdSchool,
  onCreated,
  onNext,
  onSkip,
}: SetupSchoolStepProps) {
  const handleCreate = async (data: SaveSchoolRequest) => {
    try {
      const response = await createSchool(data);
      if (response.success && response.data) {
        onCreated(response.data);
        // The district overview's school count is now stale.
        void reloadEducatorProfile();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to add school' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  return (
    <div className="space-y-6" data-testid="district-setup-school">
      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">
          Create your first school
        </h2>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          Staff and students belong to a school. Add one now to start inviting
          your team.
        </p>
      </div>

      {createdSchool ? (
        <div data-testid="district-setup-school-created">
          <Notice variant="success" title={`${createdSchool.name} created`}>
            Your first school is ready. Next, invite a staff member.
          </Notice>
        </div>
      ) : (
        <SchoolForm
          mode="create"
          submitLabel="Add school"
          onSubmit={handleCreate}
          testIdPrefix="district-setup-school"
        />
      )}

      <div className="flex gap-2">
        <Button
          onClick={onNext}
          disabled={!createdSchool}
          data-testid="district-setup-next-1"
        >
          Continue
        </Button>
        <Button variant="ghost" onClick={onSkip} data-testid="district-setup-skip-1">
          Skip for now
        </Button>
      </div>
    </div>
  );
}
