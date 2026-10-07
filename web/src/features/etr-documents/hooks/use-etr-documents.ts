import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import type { CreateEtrRequest, EtrDocument } from '../types';
import {
  create as createEtrApi,
  getById,
  listByChild,
  remove as removeEtrApi,
} from '../api/etr-documents-api';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseEtrDocumentsError = { kind: 'server'; message: string } | { kind: 'generic' };

export function useEtrDocuments(childId: number) {
  const [etrs, setEtrs] = useState<EtrDocument[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<UseEtrDocumentsError | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await listByChild(childId);
      if (response.success && response.data) {
        setEtrs(response.data);
      } else {
        setError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
      }
    } catch (err) {
      const message = apiErrorMessage(err, '');
      setError(message ? { kind: 'server', message } : { kind: 'generic' });
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
  const [error, setError] = useState<UseEtrDocumentsError | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await getById(id);
      if (response.success && response.data) {
        setEtr(response.data);
      } else {
        setError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
      }
    } catch (err) {
      const message = apiErrorMessage(err, '');
      setError(message ? { kind: 'server', message } : { kind: 'generic' });
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  return { etr, isLoading, error, reload: load };
}
