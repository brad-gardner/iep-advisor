import { Select } from '@/components/ui/input';
import {
  DISABILITY_CATEGORIES,
  DISABILITY_CATEGORY_LABELS,
  GRADE_LEVELS,
  GRADE_LEVEL_LABELS,
} from '../types';

interface EnumSelectProps {
  id: string;
  // '' means "not set".
  value: string;
  onChange: (value: string) => void;
  'data-testid'?: string;
  label?: string;
}

// Shared controlled-value pickers for the create and edit student forms so
// both surfaces list the same enum values in the same order.
export function GradeLevelSelect({ id, value, onChange, label = 'Grade', ...rest }: EnumSelectProps) {
  return (
    <Select id={id} label={label} value={value} onChange={(e) => onChange(e.target.value)} {...rest}>
      <option value="">Not set</option>
      {GRADE_LEVELS.map((grade) => (
        <option key={grade} value={grade}>
          {GRADE_LEVEL_LABELS[grade]}
        </option>
      ))}
    </Select>
  );
}

export function DisabilityCategorySelect({
  id,
  value,
  onChange,
  label = 'Disability category',
  ...rest
}: EnumSelectProps) {
  return (
    <Select id={id} label={label} value={value} onChange={(e) => onChange(e.target.value)} {...rest}>
      <option value="">Not set</option>
      {DISABILITY_CATEGORIES.map((category) => (
        <option key={category} value={category}>
          {DISABILITY_CATEGORY_LABELS[category]}
        </option>
      ))}
    </Select>
  );
}
