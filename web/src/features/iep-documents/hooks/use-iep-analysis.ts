import { useCallback, useEffect, useState } from 'react';
import type { IepAnalysis } from '@/types/api';
import { getAnalysis, triggerAnalysis } from '../api/iep-documents-api';

export function useIepAnalysis(documentId: number) {
  const [analysis, setAnalysis] = useState<IepAnalysis | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isTriggering, setIsTriggering] = useState(false);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getAnalysis(documentId);
      if (response.success && response.data) {
        setAnalysis(response.data);
      } else {
        setAnalysis(null);
      }
    } catch {
      setAnalysis(null);
    } finally {
      setIsLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    load();
  }, [load]);

  const trigger = useCallback(async () => {
    setIsTriggering(true);
    try {
      await triggerAnalysis(documentId);
      setAnalysis((prev) =>
        prev
          ? { ...prev, status: 'analyzing' }
          : {
              id: 0,
              iepDocumentId: documentId,
              status: 'analyzing',
              overallSummary: null,
              sectionAnalyses: [],
              goalAnalyses: [],
              overallRedFlags: [],
              suggestedQuestions: [],
              advocacyGapAnalysis: null,
              parentGoalsSnapshot: null,
              errorMessage: null,
              createdAt: new Date().toISOString(),
            }
      );
    } catch {
      // handled by caller
    } finally {
      setIsTriggering(false);
    }
  }, [documentId]);

  return { analysis, isLoading, isTriggering, trigger, reload: load };
}
