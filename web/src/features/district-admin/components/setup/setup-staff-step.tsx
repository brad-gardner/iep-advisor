import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ORG_ROLE } from '@/features/educator/types';
import { createStaffInvite } from '@/features/staff-invites/api/staff-invites-api';
import { InviteForm } from '@/features/staff-invites/components/invite-form';
import type { CreateStaffInviteRequest } from '@/features/staff-invites/types';
import type { DistrictSchool } from '../../types';

interface SetupStaffStepProps {
  // Schools the invite may target. Typically the single school created in step 2;
  // empty if that step was skipped (InviteForm then prompts to add one).
  schools: DistrictSchool[];
  onNext: () => void;
  onSkip: () => void;
}

// Step 3: invite the first staff member. Reuses the shared InviteForm (which
// surfaces the copyable invite URL itself). The wizard caller is always a
// DistrictAdmin. Skippable.
export function SetupStaffStep({ schools, onNext, onSkip }: SetupStaffStepProps) {
  const [invited, setInvited] = useState(false);

  const handleInvite = async (data: CreateStaffInviteRequest) => {
    try {
      const response = await createStaffInvite(data);
      if (response.success && response.data) {
        setInvited(true);
        return { success: true, invite: response.data };
      }
      return { success: false, error: response.message || 'Failed to send invite' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  return (
    <div className="space-y-6" data-testid="district-setup-staff">
      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">
          Invite your first staff member
        </h2>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          Send an invite so a school admin or teacher can join your district.
          They'll get a link to set up their own account.
        </p>
      </div>

      <InviteForm
        callerOrgRoleId={ORG_ROLE.DistrictAdmin}
        callerSchoolId={null}
        schools={schools}
        onSubmit={handleInvite}
      />

      <div className="flex gap-2">
        <Button onClick={onNext} data-testid="district-setup-next-2">
          {invited ? 'Continue' : 'Done inviting'}
        </Button>
        <Button variant="ghost" onClick={onSkip} data-testid="district-setup-skip-2">
          Skip for now
        </Button>
      </div>
    </div>
  );
}
