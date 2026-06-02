import { useCallback, useEffect, useState } from 'react';
import * as api from '../api/iep-drafts-api';
import type { IepDraftDto } from '../types';

interface UseIepDraftListResult {
  drafts: IepDraftDto[];
  isLoading: boolean;
  error: string | null;
  creating: boolean;
  // Creates a draft and returns its id (for navigation), or null on failure.
  create: () => Promise<number | null>;
}

export function useIepDraftList(studentId: number): UseIepDraftListResult {
  const [drafts, setDrafts] = useState<IepDraftDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    // isLoading/error already start in their pending state, so the effect only
    // resolves them — avoiding a synchronous setState in the effect body.
    let cancelled = false;
    api
      .listDrafts(studentId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setDrafts(res.data);
        else setError(res.message ?? 'Failed to load IEP drafts');
      })
      .catch(() => {
        if (!cancelled) setError('Failed to load IEP drafts');
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [studentId]);

  const create = useCallback(async (): Promise<number | null> => {
    setCreating(true);
    try {
      const res = await api.createDraft(studentId, {});
      if (res.success && res.data) {
        setDrafts((prev) => [res.data as IepDraftDto, ...prev]);
        return res.data.id;
      }
      return null;
    } catch {
      return null;
    } finally {
      setCreating(false);
    }
  }, [studentId]);

  return { drafts, isLoading, error, creating, create };
}
