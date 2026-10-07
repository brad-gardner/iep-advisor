import i18n from '@/lib/i18n';
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

// The four "manual" sections the student adds to directly, in display order.
// The fifth kind, AiInterviewAnswer, is produced by the AI interview helper
// and surfaced within the Meeting Statements section (see groupEntries in
// `student-home-page.tsx`) — it has a translated `entryKindLabel` (used by
// `features/meeting-prep` and `features/document-authoring`'s pull-from-
// student pickers) but no section of its own (no `sectionTitle`/`hint`/
// `placeholder` key — only `label`), hence the narrower type here: without
// it, `kind` inside `getEntryKinds`' map would still include
// `AiInterviewAnswer`, and `tsc` would correctly refuse the `.sectionTitle`/
// `.hint`/`.placeholder` template-literal keys since that kind has none.
type ManualEntryKind = Exclude<StudentWorkspaceEntryKind, 'AiInterviewAnswer'>;
const MANUAL_ENTRY_KIND_ORDER: ManualEntryKind[] = [
  'Strength',
  'Interest',
  'AccommodationRequest',
  'MeetingStatement',
];

/**
 * Translated label for a workspace entry kind — via `student:entryKinds.
 * <Kind>.label`. Same shape as `meetingTypeLabel`/`inviteStatusLabel`: a
 * plain function over `i18n.t`, callable from render bodies and plain code
 * alike. Every `StudentWorkspaceEntryKind` (including `AiInterviewAnswer`,
 * which has no section of its own) has a translation.
 */
export function entryKindLabel(kind: StudentWorkspaceEntryKind): string {
  return i18n.t(`student:entryKinds.${kind}.label`);
}

/**
 * The four manual sections' full metadata, translated fresh on every call —
 * call this from a render body (in a component that itself calls
 * `useTranslation`, so it re-renders on a language switch), never cache the
 * result across renders.
 */
export function getEntryKinds(): EntryKindMeta[] {
  return MANUAL_ENTRY_KIND_ORDER.map((kind) => ({
    kind,
    label: entryKindLabel(kind),
    sectionTitle: i18n.t(`student:entryKinds.${kind}.sectionTitle`),
    hint: i18n.t(`student:entryKinds.${kind}.hint`),
    placeholder: i18n.t(`student:entryKinds.${kind}.placeholder`),
  }));
}
