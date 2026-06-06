import type { IepSectionKind } from '../types';

export const SECTION_KINDS: { value: IepSectionKind; label: string }[] = [
  { value: 'StudentProfile', label: 'Student Profile' },
  { value: 'PresentLevels', label: 'Present Levels' },
  { value: 'Eligibility', label: 'Eligibility' },
  { value: 'Placement', label: 'Placement' },
  { value: 'ProgressMonitoring', label: 'Progress Monitoring' },
  { value: 'SpecialFactors', label: 'Special Factors' },
  { value: 'Other', label: 'Other' },
];

export function sectionKindLabel(kind: string): string {
  return SECTION_KINDS.find((k) => k.value === kind)?.label ?? kind;
}
