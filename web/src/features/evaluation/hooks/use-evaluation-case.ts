import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getEvaluationCase } from '../api/evaluation-api';
import type { EvaluationCaseDto } from '../types';

interface UseEvaluationCaseResult {
  evaluation: EvaluationCaseDto | null;
  /** True only until the first load settles; `evaluation` staying `null`
   *  afterwards means "no case yet" (a valid, non-error state). */
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  /** Adopt a server response after any mutation (create/consent/assignment/…). */
  applyUpdate: (updated: EvaluationCaseDto) => void;
}

/** A student's evaluation case (open, else most recent closed, else none)
 *  for the "Evaluation" card on the educator student page. */
export function useEvaluationCase(studentId: number): UseEvaluationCaseResult {
  const [evaluation, setEvaluation] = useState<EvaluationCaseDto | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    if (!studentId) return;
    let active = true;
    (async () => {
      try {
        const res = await getEvaluationCase(studentId);
        if (!active) return;
        if (res.success) {
          setEvaluation(res.data ?? null);
          setError(null);
        } else {
          setError(res.message ?? 'Could not load the evaluation case.');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load the evaluation case.'));
      } finally {
        if (active) setHasLoaded(true);
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId, retryToken]);

  const applyUpdate = useCallback((updated: EvaluationCaseDto) => {
    setEvaluation(updated);
  }, []);

  return {
    evaluation,
    isLoading: !hasLoaded && error === null,
    error,
    retry: () => {
      setHasLoaded(false);
      setRetryToken((t) => t + 1);
    },
    applyUpdate,
  };
}
