import { useTranslation } from 'react-i18next';
import { Select } from '@/components/ui/input';
import { disabilityCategoryLabel } from '@/lib/disability-category-label';
import { gradeLevelLabel } from '@/lib/grade-level-label';
import { DISABILITY_CATEGORIES, GRADE_LEVELS } from '../types';

interface EnumSelectProps {
  id: string;
  // '' means "not set".
  value: string;
  onChange: (value: string) => void;
  'data-testid'?: string;
  label?: string;
}

// Shared controlled-value pickers for the create and edit student forms so
// both surfaces list the same enum values in the same order. Option labels
// go through the shared, translated `gradeLevelLabel`/`disabilityCategoryLabel`
// helpers (`docs/i18n/README.md`'s "Display-label helpers follow
// `orgRoleLabel`'s shape") rather than the raw `GRADE_LEVEL_LABELS`/
// `DISABILITY_CATEGORY_LABELS` maps, which stay English (the stored/
// canonical values) — reused here instead of duplicating a staff-only
// translation.
export function GradeLevelSelect({ id, value, onChange, label, ...rest }: EnumSelectProps) {
  const { t } = useTranslation(['educator', 'common']);
  return (
    <Select id={id} label={label ?? t('educator:enumSelects.gradeLabel')} value={value} onChange={(e) => onChange(e.target.value)} {...rest}>
      <option value="">{t('common:ui.notSet')}</option>
      {GRADE_LEVELS.map((grade) => (
        <option key={grade} value={grade}>
          {gradeLevelLabel(grade)}
        </option>
      ))}
    </Select>
  );
}

export function DisabilityCategorySelect({ id, value, onChange, label, ...rest }: EnumSelectProps) {
  const { t } = useTranslation(['educator', 'common']);
  return (
    <Select
      id={id}
      label={label ?? t('educator:enumSelects.disabilityLabel')}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      {...rest}
    >
      <option value="">{t('common:ui.notSet')}</option>
      {DISABILITY_CATEGORIES.map((category) => (
        <option key={category} value={category}>
          {disabilityCategoryLabel(category)}
        </option>
      ))}
    </Select>
  );
}
