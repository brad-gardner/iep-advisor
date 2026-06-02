// TypeScript mirrors of the P6b backend AI-assist contract.

export type AssistKind = 'Rewrite' | 'Improve' | 'SuggestMeasurement';

export interface AssistRequest {
  kind: AssistKind;
}

export interface AssistResponse {
  suggestion: string;
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
