export type JournalTag = 'Incident' | 'Communication' | 'Medical' | 'Progress' | 'Other';

export const JOURNAL_TAGS: JournalTag[] = ['Incident', 'Communication', 'Medical', 'Progress', 'Other'];

export const JOURNAL_TAG_LABELS: Record<JournalTag, string> = {
  Incident: 'Incident',
  Communication: 'Communication',
  Medical: 'Medical',
  Progress: 'Progress',
  Other: 'Other',
};

/** Serialized markdown cap — the same number the API enforces. */
export const JOURNAL_CONTENT_MAX_LENGTH = 4000;

export interface JournalEntryDto {
  id: number;
  childProfileId: number;
  /** yyyy-MM-dd — the day the entry is about, not when it was written. */
  occurredOn: string;
  tag: JournalTag;
  contentMarkdown: string;
  linkedIepDocumentId?: number | null;
  linkedEtrDocumentId?: number | null;
  linkedMeetingId?: number | null;
  createdAt: string;
  updatedAt: string;
  createdById?: number | null;
}

export interface SaveJournalEntryRequest {
  occurredOn: string;
  tag: JournalTag;
  contentMarkdown: string;
  linkedIepDocumentId?: number | null;
  linkedEtrDocumentId?: number | null;
  linkedMeetingId?: number | null;
}
