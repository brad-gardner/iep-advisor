import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { createAdvocateThread, deleteAdvocateThread, listAdvocateThreads, renameAdvocateThread } from '../api/advocate-api';
import type { AdvocateThreadDto } from '../types/advocate';

const LOAD_ERROR = 'Could not load your conversations.';

/**
 * The parent's own threads for one child, newest activity first. Mutations
 * update the list in place after the server confirms; `touch` bumps a thread
 * to the top when a message lands so the rail matches what a reload would
 * show without refetching.
 */
export function useAdvocateThreads(childId: number) {
  const [threads, setThreads] = useState<AdvocateThreadDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
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
          setError(res.message ?? LOAD_ERROR);
        }
      })
      .catch((err) => {
        if (active) setError(apiErrorMessage(err, LOAD_ERROR));
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
        throw new Error(apiErrorMessage(err, 'Could not start a conversation.'));
      }
      if (!res.success || !res.data) throw new Error(res.message ?? 'Could not start a conversation.');
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
      throw new Error(apiErrorMessage(err, 'Could not rename this conversation.'));
    }
    if (!res.success) throw new Error(res.message ?? 'Could not rename this conversation.');
    setThreads((list) => (list ?? []).map((t) => (t.id === id ? { ...t, title } : t)));
  }, []);

  const remove = useCallback(async (id: number): Promise<void> => {
    let res;
    try {
      res = await deleteAdvocateThread(id);
    } catch (err) {
      throw new Error(apiErrorMessage(err, 'Could not delete this conversation.'));
    }
    if (!res.success) throw new Error(res.message ?? 'Could not delete this conversation.');
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
