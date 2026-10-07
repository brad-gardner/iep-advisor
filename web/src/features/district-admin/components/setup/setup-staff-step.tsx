import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('district-admin');
  const [invited, setInvited] = useState(false);

  const handleInvite = async (data: CreateStaffInviteRequest) => {
    try {
      const response = await createStaffInvite(data);
      if (response.success && response.data) {
        setInvited(true);
        return { success: true, invite: response.data };
      }
      return { success: false, error: response.message || t('setupWizard.staff.errorFailed') };
    } catch {
      return { success: false, error: t('setupWizard.staff.errorGeneric') };
    }
  };

  return (
    <div className="space-y-6" data-testid="district-setup-staff">
      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">
          {t('setupWizard.staff.heading')}
        </h2>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          {t('setupWizard.staff.body')}
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
          {invited ? t('setupWizard.staff.continue') : t('setupWizard.staff.doneInviting')}
        </Button>
        <Button variant="ghost" onClick={onSkip} data-testid="district-setup-skip-2">
          {t('setupWizard.staff.skip')}
        </Button>
      </div>
    </div>
  );
}
