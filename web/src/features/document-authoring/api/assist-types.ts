// TypeScript mirrors of the P6b backend AI-assist contract.

export type AssistKind = 'Rewrite' | 'Improve' | 'SuggestMeasurement';

export interface AssistRequest {
  kind: AssistKind;
}

export interface AssistCitation {
  evidenceId: string;
  sourceLabel: string;
  excerpt: string;
}

export interface AssistResponse {
  suggestion: string;
  /** Present when the request was evidence-grounded (document assist). */
  rationale?: string | null;
  citations?: AssistCitation[];
  /** The target is a goal with no baseline and no baseline in the evidence: ask for data, don't invent. */
  missingBaseline?: boolean;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatRequest {
  messages: ChatMessage[];
}

export interface ChatResponse {
  reply: string;
}

export const ASSIST_KIND_LABELS: Record<AssistKind, string> = {
  Rewrite: 'Rewrite',
  Improve: 'Improve',
  SuggestMeasurement: 'Suggest measurement',
};
