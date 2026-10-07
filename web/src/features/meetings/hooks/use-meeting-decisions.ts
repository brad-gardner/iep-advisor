import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/lib/api-error';
import { getDecisions } from '../api/meeting-decisions-api';
import type { MeetingDecisionDto } from '../types';

// A server-provided message is already resolved text and is shown as-is;
// the generic fallback is translated at RENDER time (below), not stored
// pre-translated here, so the mount effect never needs `t` in its
// dependency array (same idiom as `useHome`/`MeetingRsvpPage`'s `LoadError`).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

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
  const { t } = useTranslation('meetings-staff');
  const [decisions, setDecisions] = useState<MeetingDecisionDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);
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
          setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): re-running this fetch on
    // a plain language switch would be wasteful.
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
    setRetryToken((n) => n + 1);
  }, []);

  return {
    decisions,
    isLoading,
    error: error ? (error.kind === 'server' ? error.message : t('decisionsPanel.loadFailed')) : null,
    retry,
    addDecision,
    updateDecision,
    removeDecision,
  };
}
