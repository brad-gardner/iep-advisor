import { useCallback, useEffect, useState } from 'react';
import type { CreateEtrRequest, EtrDocument } from '../types';
import {
  create as createEtrApi,
  getById,
  listByChild,
  remove as removeEtrApi,
} from '../api/etr-documents-api';

export function useEtrDocuments(childId: number) {
  const [etrs, setEtrs] = useState<EtrDocument[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await listByChild(childId);
      if (response.success && response.data) {
        setEtrs(response.data);
      } else {
        setError(response.message || 'Failed to load ETRs');
      }
    } catch {
      setError('Failed to load ETRs');
    } finally {
      setIsLoading(false);
    }
  }, [childId]);

  useEffect(() => {
    load();
  }, [load]);

  const create = useCallback(
    async (payload: CreateEtrRequest) => {
      const response = await createEtrApi(childId, payload);
      if (response.success) {
        await load();
      }
      return response;
    },
    [childId, load]
  );

  const remove = useCallback(
    async (id: number) => {
      const response = await removeEtrApi(id);
      if (response.success) {
        await load();
      }
      return response;
    },
    [load]
  );

  return { etrs, isLoading, error, reload: load, refresh: load, create, remove };
}

export function useEtrDocument(id: number) {
  const [etr, setEtr] = useState<EtrDocument | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await getById(id);
      if (response.success && response.data) {
        setEtr(response.data);
      } else {
        setError(response.message || 'ETR not found');
      }
    } catch {
      setError('Failed to load ETR');
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  return { etr, isLoading, error, reload: load };
}
