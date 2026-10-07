import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('district-admin');
  const handleCreate = async (data: SaveSchoolRequest) => {
    try {
      const response = await createSchool(data);
      if (response.success && response.data) {
        onCreated(response.data);
        // The district overview's school count is now stale.
        void reloadEducatorProfile();
        return { success: true };
      }
      return { success: false, error: response.message || t('setupWizard.school.errorFailed') };
    } catch {
      return { success: false, error: t('setupWizard.school.errorGeneric') };
    }
  };

  return (
    <div className="space-y-6" data-testid="district-setup-school">
      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">
          {t('setupWizard.school.heading')}
        </h2>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          {t('setupWizard.school.body')}
        </p>
      </div>

      {createdSchool ? (
        <div data-testid="district-setup-school-created">
          <Notice variant="success" title={t('setupWizard.school.createdTitle', { name: createdSchool.name })}>
            {t('setupWizard.school.createdBody')}
          </Notice>
        </div>
      ) : (
        <SchoolForm
          mode="create"
          submitLabel={t('setupWizard.school.addSchool')}
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
          {t('setupWizard.school.continue')}
        </Button>
        <Button variant="ghost" onClick={onSkip} data-testid="district-setup-skip-1">
          {t('setupWizard.school.skip')}
        </Button>
      </div>
    </div>
  );
}
