export const ETR_SECTION_TYPE_LABELS: Record<string, string> = {
  referral_reason: 'Referral Reason',
  background_information: 'Background Information',
  parent_input: 'Parent Input',
  teacher_input: 'Teacher Input',
  student_input: 'Student Input',
  health_vision_hearing: 'Health, Vision & Hearing',
  cognitive_assessment: 'Cognitive Assessment',
  academic_assessment: 'Academic Assessment',
  behavioral_social_emotional: 'Behavioral / Social-Emotional',
  speech_language: 'Speech & Language',
  occupational_physical_therapy: 'Occupational / Physical Therapy',
  adaptive_functional: 'Adaptive / Functional',
  eligibility_determination: 'Eligibility Determination',
  other: 'Other',
};

export function formatSectionTypeLabel(sectionType: string): string {
  return (
    ETR_SECTION_TYPE_LABELS[sectionType] ||
    sectionType
      .split('_')
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ')
  );
}
