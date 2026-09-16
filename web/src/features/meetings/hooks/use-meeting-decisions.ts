import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getDecisions } from '../api/meeting-decisions-api';
import type { MeetingDecisionDto } from '../types';

interface UseMeetingDecisionsResult {
  decisions: MeetingDecisionDto[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  addDecision: (decision: MeetingDecisionDto) => void;
  updateDecision: (decision: MeetingDecisionDto) => void;
  removeDecision: (id: number) => void;
}

/** A Held/Continued meeting's structured decisions (plan 7, decision 3). */
export function useMeetingDecisions(meetingId: number): UseMeetingDecisionsResult {
  const [decisions, setDecisions] = useState<MeetingDecisionDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    if (!meetingId) return;
    let active = true;
    (async () => {
      try {
        const res = await getDecisions(meetingId);
        if (!active) return;
        if (res.success && res.data) {
          setDecisions(res.data);
          setError(null);
        } else {
          setError(res.message ?? 'Could not load decisions.');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load decisions.'));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [meetingId, retryToken]);

  const addDecision = useCallback((decision: MeetingDecisionDto) => {
    setDecisions((prev) => [decision, ...prev]);
  }, []);

  const updateDecision = useCallback((decision: MeetingDecisionDto) => {
    setDecisions((prev) => prev.map((d) => (d.id === decision.id ? decision : d)));
  }, []);

  const removeDecision = useCallback((id: number) => {
    setDecisions((prev) => prev.filter((d) => d.id !== id));
  }, []);

  const retry = useCallback(() => {
    setIsLoading(true);
    setError(null);
    setRetryToken((t) => t + 1);
  }, []);

  return {
    decisions,
    isLoading,
    error,
    retry,
    addDecision,
    updateDecision,
    removeDecision,
  };
}
