import type { StudentWorkspaceEntryKind } from '../types';

export interface EntryKindMeta {
  kind: StudentWorkspaceEntryKind;
  label: string;
  // Section heading shown to the student.
  sectionTitle: string;
  // Short helper line describing what belongs in this section.
  hint: string;
  // Placeholder for the add/edit textarea.
  placeholder: string;
}

// The four "manual" sections the student adds to directly. The fifth kind,
// AiInterviewAnswer, is produced by the AI interview helper and surfaced
// within the Meeting Statements section (see groupEntries).
export const ENTRY_KINDS: EntryKindMeta[] = [
  {
    kind: 'Strength',
    label: 'Strength',
    sectionTitle: 'My Strengths',
    hint: 'Things you are good at or proud of.',
    placeholder: 'I am good at…',
  },
  {
    kind: 'Interest',
    label: 'Interest',
    sectionTitle: 'My Interests',
    hint: 'Things you enjoy or want to learn more about.',
    placeholder: 'I am interested in…',
  },
  {
    kind: 'AccommodationRequest',
    label: 'Accommodation request',
    sectionTitle: 'Accommodations I Want',
    hint: 'Supports that help you learn best.',
    placeholder: 'It helps me when…',
  },
  {
    kind: 'MeetingStatement',
    label: 'Meeting statement',
    sectionTitle: 'What I Want to Say in My Meeting',
    hint: 'Things you want your team to hear.',
    placeholder: 'At my meeting, I want to say…',
  },
];

export function entryKindLabel(kind: StudentWorkspaceEntryKind): string {
  return ENTRY_KINDS.find((k) => k.kind === kind)?.label ?? kind;
}
