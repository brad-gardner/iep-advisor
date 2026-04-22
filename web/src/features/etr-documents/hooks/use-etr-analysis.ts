import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { usePolling } from '@/hooks/use-polling';
import { getAnalysis, startAnalysis as startAnalysisApi } from '../api/etr-documents-api';
import type { EtrAnalysis, EtrAnalysisStatus } from '../types';

export type UseEtrAnalysisStatus = 'none' | EtrAnalysisStatus;

export interface UseEtrAnalysisResult {
  analysis: EtrAnalysis | null;
  status: UseEtrAnalysisStatus;
  loading: boolean;
  isTriggering: boolean;
  error: string | null;
  start: () => Promise<void>;
  refresh: () => Promise<void>;
}

export function useEtrAnalysis(etrId: number): UseEtrAnalysisResult {
  const [analysis, setAnalysis] = useState<EtrAnalysis | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [isTriggering, setIsTriggering] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await getAnalysis(etrId);
      if (response.success && response.data) {
        setAnalysis(response.data);
      } else {
        setAnalysis(null);
      }
    } catch (err: unknown) {
      // 404 means "no analysis yet"
      if (axios.isAxiosError(err) && err.response?.status === 404) {
        setAnalysis(null);
      } else {
        setError(
          axios.isAxiosError(err)
            ? err.response?.data?.message || err.message
            : 'Failed to load analysis'
        );
        setAnalysis(null);
      }
    } finally {
      setLoading(false);
    }
  }, [etrId]);

  useEffect(() => {
    void load();
  }, [load]);

  const start = useCallback(async () => {
    setIsTriggering(true);
    setError(null);
    try {
      const response = await startAnalysisApi(etrId);
      if (!response.success) {
        setError(response.message || 'Failed to start analysis');
        return;
      }
      // Optimistically flip to analyzing so the UI shows the processing view.
      setAnalysis((prev) =>
        prev
          ? { ...prev, status: 'analyzing', errorMessage: null }
          : {
              id: 0,
              etrDocumentId: etrId,
              status: 'analyzing',
              assessmentCompleteness: null,
              eligibilityReview: null,
              overallRedFlags: [],
              suggestedQuestions: [],
              overallSummary: null,
              errorMessage: null,
              createdAt: new Date().toISOString(),
            }
      );
    } catch (err: unknown) {
      setError(
        axios.isAxiosError(err)
          ? err.response?.data?.message || err.message
          : 'Failed to start analysis'
      );
    } finally {
      setIsTriggering(false);
    }
  }, [etrId]);

  // Poll while analysis is pending/analyzing
  const pollStatus = useCallback(async () => {
    try {
      const response = await getAnalysis(etrId);
      if (response.success && response.data) {
        setAnalysis(response.data);
      }
    } catch {
      // swallow errors during polling; they surface on explicit refresh
    }
  }, [etrId]);

  const isInProgress =
    analysis?.status === 'analyzing' || analysis?.status === 'pending';
  usePolling(pollStatus, 3000, isInProgress);

  const status: UseEtrAnalysisStatus = analysis ? analysis.status : 'none';

  return {
    analysis,
    status,
    loading,
    isTriggering,
    error,
    start,
    refresh: load,
  };
}
