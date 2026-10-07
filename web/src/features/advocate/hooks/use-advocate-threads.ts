import { useCallback, useEffect, useState } from 'react';
import { type LoadError, apiErrorMessage, toLoadError } from '@/lib/api-error';
import i18n from '@/lib/i18n';
import { createAdvocateThread, deleteAdvocateThread, listAdvocateThreads, renameAdvocateThread } from '../api/advocate-api';
import type { AdvocateThreadDto } from '../types/advocate';

// Live `i18n.t()` calls (not frozen constants) — see `docs/i18n/README.md`'s
// "Display-label helpers" pattern.
export function threadsCreateFailed(): string {
  return i18n.t('advocate:threads.createFailed');
}
export function threadsRenameFailed(): string {
  return i18n.t('advocate:threads.renameFailed');
}
export function threadsDeleteFailed(): string {
  return i18n.t('advocate:threads.deleteFailed');
}

/**
 * The parent's own threads for one child, newest activity first. Mutations
 * update the list in place after the server confirms; `touch` bumps a thread
 * to the top when a message lands so the rail matches what a reload would
 * show without refetching.
 */
export function useAdvocateThreads(childId: number) {
  const [threads, setThreads] = useState<AdvocateThreadDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let active = true;
    listAdvocateThreads(childId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) {
          setThreads(res.data);
          setError(null);
        } else {
          setError(toLoadError(res));
        }
      })
      .catch((err) => {
        if (!active) return;
        setError(toLoadError(err));
      });
    return () => {
      active = false;
    };
  }, [childId, reloadToken]);

  const reload = useCallback(() => setReloadToken((t) => t + 1), []);

  /** Resolves with the new thread; throws with a user-facing message otherwise. */
  const create = useCallback(
    async (title?: string): Promise<AdvocateThreadDto> => {
      let res;
      try {
        res = await createAdvocateThread(childId, title);
      } catch (err) {
        throw new Error(apiErrorMessage(err, threadsCreateFailed()));
      }
      if (!res.success || !res.data) throw new Error(res.message ?? threadsCreateFailed());
      const created = res.data;
      setThreads((list) => [created, ...(list ?? [])]);
      return created;
    },
    [childId],
  );

  const rename = useCallback(async (id: number, title: string): Promise<void> => {
    let res;
    try {
      res = await renameAdvocateThread(id, title);
    } catch (err) {
      throw new Error(apiErrorMessage(err, threadsRenameFailed()));
    }
    if (!res.success) throw new Error(res.message ?? threadsRenameFailed());
    setThreads((list) => (list ?? []).map((t) => (t.id === id ? { ...t, title } : t)));
  }, []);

  const remove = useCallback(async (id: number): Promise<void> => {
    let res;
    try {
      res = await deleteAdvocateThread(id);
    } catch (err) {
      throw new Error(apiErrorMessage(err, threadsDeleteFailed()));
    }
    if (!res.success) throw new Error(res.message ?? threadsDeleteFailed());
    setThreads((list) => (list ?? []).filter((t) => t.id !== id));
  }, []);

  const touch = useCallback((id: number) => {
    const now = new Date().toISOString();
    setThreads((list) => {
      if (!list) return list;
      const hit = list.find((t) => t.id === id);
      if (!hit) return list;
      return [{ ...hit, lastMessageAt: now, updatedAt: now }, ...list.filter((t) => t.id !== id)];
    });
  }, []);

  return { threads, error, reload, create, rename, remove, touch };
}
