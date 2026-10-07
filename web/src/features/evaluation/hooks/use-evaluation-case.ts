import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type LoadError, toLoadError, loadErrorText } from '@/lib/api-error';
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
  const { t } = useTranslation('evaluation');
  const [evaluation, setEvaluation] = useState<EvaluationCaseDto | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [error, setError] = useState<LoadError | null>(null);
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
          setError(toLoadError(res));
        }
      } catch (err) {
        if (!active) return;
        setError(toLoadError(err));
      } finally {
        if (active) setHasLoaded(true);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): re-running this fetch on
    // a plain language switch would be wasteful. The generic fallback is
    // translated below, at render, from `error`'s stored KIND rather than a
    // snapshot string.
  }, [studentId, retryToken]);

  const applyUpdate = useCallback((updated: EvaluationCaseDto) => {
    setEvaluation(updated);
  }, []);

  return {
    evaluation,
    isLoading: !hasLoaded && error === null,
    error: loadErrorText(error, t('card.loadFailed')),
    retry: () => {
      setHasLoaded(false);
      setRetryToken((n) => n + 1);
    },
    applyUpdate,
  };
}
