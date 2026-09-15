import { useCallback, useState } from 'react';
import type { ApiResponse } from '@/types/api';
import type { AssistCitation, AssistKind, AssistResponse } from '../api/assist-types';
import { friendlyAssistError } from '../lib/assist-errors';

export type FieldAssistStatus = 'idle' | 'loading' | 'suggested' | 'applied' | 'error';

export interface UseFieldAssistResult {
  status: FieldAssistStatus;
  suggestion: string | null;
  rationale: string | null;
  citations: AssistCitation[];
  missingBaseline: boolean;
  errorMessage: string | null;
  request: (kind: AssistKind) => void;
  // The hook stays ignorant of which field: the caller supplies how to apply
  // the suggestion (e.g. route it through the row's existing patch + autosave).
  accept: (onApply: (text: string) => void) => void;
  dismiss: () => void;
}

// Drives a single inline assist interaction for one field. `requestFn` is the
// API call bound to the specific row (goal/section/service line); this hook only
// tracks the request lifecycle and never touches the draft itself.
export function useFieldAssist(
  requestFn: (kind: AssistKind) => Promise<ApiResponse<AssistResponse>>
): UseFieldAssistResult {
  const [status, setStatus] = useState<FieldAssistStatus>('idle');
  const [suggestion, setSuggestion] = useState<string | null>(null);
  const [rationale, setRationale] = useState<string | null>(null);
  const [citations, setCitations] = useState<AssistCitation[]>([]);
  const [missingBaseline, setMissingBaseline] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const request = useCallback(
    (kind: AssistKind) => {
      setStatus('loading');
      setErrorMessage(null);
      setSuggestion(null);
      setRationale(null);
      setCitations([]);
      setMissingBaseline(false);
      requestFn(kind)
        .then((res) => {
          if (res.success && res.data) {
            setSuggestion(res.data.suggestion);
            setRationale(res.data.rationale ?? null);
            setCitations(res.data.citations ?? []);
            setMissingBaseline(res.data.missingBaseline === true);
            setStatus('suggested');
          } else {
            setErrorMessage(res.message || 'AI help is unavailable right now.');
            setStatus('error');
          }
        })
        .catch((err: unknown) => {
          setErrorMessage(friendlyAssistError(err));
          setStatus('error');
        });
    },
    [requestFn]
  );

  // Hand the suggestion to the caller's applier (which owns the field), then
  // mark applied. Keeps the hook decoupled from any specific draft field.
  const accept = useCallback(
    (onApply: (text: string) => void) => {
      if (suggestion === null) return;
      onApply(suggestion);
      setStatus('applied');
    },
    [suggestion]
  );

  const dismiss = useCallback(() => {
    setStatus('idle');
    setSuggestion(null);
    setRationale(null);
    setCitations([]);
    setMissingBaseline(false);
    setErrorMessage(null);
  }, []);

  return { status, suggestion, rationale, citations, missingBaseline, errorMessage, request, accept, dismiss };
}
