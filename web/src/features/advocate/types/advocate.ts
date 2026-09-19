/** Mirrors api/IepAssistant.Api/DTOs/Advocate/AdvocateDtos.cs. */

export interface AdvocateThreadDto {
  id: number;
  childProfileId: number;
  title: string;
  createdAt: string;
  updatedAt: string;
  lastMessageAt: string;
}

export type AdvocateRole = 'User' | 'Assistant';

/** The record a citation belongs to (a goal's IEP, a section's document, an analysis's document). */
export interface AdvocateCitationParent {
  kind: string;
  id: number;
}

/**
 * One source the answer drew on. `kind` is one of `CITATION_KINDS` (unknown
 * kinds render as plain chips); `parent` is set for goal, iep_section,
 * etr_section, progress_report and the *_analysis kinds.
 */
export interface AdvocateCitation {
  kind: string;
  id: number;
  label?: string | null;
  parent?: AdvocateCitationParent | null;
}

/** Every `kind` the server can return in `citations` — see AdvocateToolset.Register. */
export const CITATION_KINDS = [
  'kb',
  'child',
  'iep',
  'etr',
  'progress_report',
  'authored_version',
  'shared_draft',
  'iep_analysis',
  'etr_analysis',
  'progress_report_analysis',
  'analysis_run',
  'iep_section',
  'etr_section',
  'goal',
  'goal_record',
  'comparison',
  'journal',
  'contribution',
  'advocacy_goal',
  'meeting_prep',
  'meeting',
] as const;

export type CitationKind = (typeof CITATION_KINDS)[number];

export type AdvocateSuggestionKind = 'prep_question' | 'journal_entry' | 'open_kb' | 'open_goal';

export interface AdvocateSuggestion {
  /** One of `AdvocateSuggestionKind`; unknown kinds are ignored by the UI. */
  kind: AdvocateSuggestionKind | string;
  text?: string | null;
  id?: number | null;
  /** yyyy-MM-dd, `journal_entry` only. */
  date?: string | null;
}

export interface AdvocateMessageDto {
  id: number;
  role: AdvocateRole;
  contentMarkdown: string;
  citations: AdvocateCitation[];
  suggestions: AdvocateSuggestion[];
  truncated: boolean;
  createdAt: string;
}

export interface AdvocateThreadDetailDto extends AdvocateThreadDto {
  /** Oldest first. */
  messages: AdvocateMessageDto[];
  disclaimer: string;
}

export interface AdvocateUsageDto {
  used: number;
  limit: number;
  subscriptionActive: boolean;
}

export interface SendAdvocateMessageRequest {
  text: string;
  /** Launcher context, e.g. `iep:12` — see `ABOUT_PATTERN`. */
  about?: string;
}

// ---- SSE frame payloads (event: delta | tool | done | error) ----

export interface AdvocateDeltaFrame {
  text: string;
}

export type AdvocateToolStatus = 'started' | 'finished' | 'failed';

export interface AdvocateToolFrame {
  name: string;
  label: string;
  status: AdvocateToolStatus;
}

export interface AdvocateDoneFrame {
  messageId: number;
  contentMarkdown: string;
  citations: AdvocateCitation[];
  suggestions: AdvocateSuggestion[];
  truncated: boolean;
  disclaimer: string;
}

export interface AdvocateErrorFrame {
  /** `unavailable` mid-stream; the pre-stream codes come back as HTTP statuses instead. */
  code: string;
  message: string;
}

/** Same cap the API enforces on `SendAdvocateMessageRequest.Text`. */
export const ADVOCATE_MESSAGE_MAX_LENGTH = 2000;

/** Same cap the API enforces on a thread title. */
export const ADVOCATE_TITLE_MAX_LENGTH = 120;

/** The server grammar for `about`; anything else is dropped client-side rather than sent. */
export const ABOUT_PATTERN = /^(iep|etr|goal|analysis|progress_report|journal):\d+$/;

/** Share of the yearly allowance at which the usage banner appears. */
export const USAGE_WARNING_RATIO = 0.8;
