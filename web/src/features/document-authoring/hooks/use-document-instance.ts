import { useCallback, useEffect, useRef, useState } from 'react';
import { AxiosError } from 'axios';
import type { ApiResponse } from '@/types/api';
import type { AutosaveStatus } from '@/features/iep-authoring/hooks/use-autosave';
import { getDocument, saveValues as saveValuesApi } from '../api/documents-api';
import type { DocumentInstanceDetailDto, DocumentValuePatch } from '../types';

export interface SaveResult {
  ok: boolean;
  /** Stale rowVersion (409) — the editor must reload before further edits. */
  conflict?: boolean;
  errors?: string[];
  message?: string;
}

const OK: SaveResult = { ok: true };
const SAVED_LINGER_MS = 1500;

/**
 * Holds one document instance's pinned template tree + values + rowVersion,
 * loads it on mount, and exposes a SERIALIZED `saveValues(patch)`.
 *
 * The instance's rowVersion is one optimistic-concurrency token for the whole
 * value-document, so two in-flight PUTs would race and 409 each other. Every
 * save runs through a single promise chain and reads the freshest rowVersion
 * from a ref at execution time (mirrors the admin template builder). Each save
 * response returns the refreshed detail + a fresh rowVersion, which simply
 * replaces both. A 409 sets `conflict`; the editor prompts a reload.
 *
 * `saveStatus` is an aggregate autosave pill for the header, derived from the
 * serialized runner (per-field debouncing lives in each field via use-autosave).
 */
export function useDocumentInstance(instanceId: number) {
  const [detail, setDetail] = useState<DocumentInstanceDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const [saveStatus, setSaveStatus] = useState<AutosaveStatus>('idle');

  // Always read the freshest detail/rowVersion at save-execution time so a
  // queued op never sends a stale token.
  const detailRef = useRef<DocumentInstanceDetailDto | null>(null);
  const setDetailTree = useCallback((d: DocumentInstanceDetailDto) => {
    detailRef.current = d;
    setDetail(d);
  }, []);

  // Promise chain that serializes every save.
  const chainRef = useRef<Promise<unknown>>(Promise.resolve());
  const pendingRef = useRef(0);
  // Mirrors `conflict` so a queued save reads the latched value at execution
  // time (closure state would be stale for saves queued in the same batch).
  const conflictRef = useRef(false);
  const latchConflict = useCallback((v: boolean) => {
    conflictRef.current = v;
    setConflict(v);
  }, []);
  const savedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (savedTimerRef.current) clearTimeout(savedTimerRef.current);
    };
  }, []);

  // Load on mount and whenever reload() bumps reloadKey. The body only setState
  // after an await (or in the pending pre-state), so it stays effect-safe.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await getDocument(instanceId);
        if (cancelled) return;
        setLoadError(null);
        latchConflict(false);
        if (res.success && res.data) setDetailTree(res.data);
        else setLoadError(res.message ?? 'Failed to load document.');
      } catch {
        if (!cancelled) setLoadError('Failed to load document.');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [instanceId, reloadKey, setDetailTree, latchConflict]);

  const reload = useCallback(() => {
    setIsLoading(true);
    setLoadError(null);
    latchConflict(false);
    setSaveStatus('idle');
    setReloadKey((k) => k + 1);
  }, [latchConflict]);

  const settleStatus = useCallback((ok: boolean) => {
    pendingRef.current -= 1;
    if (pendingRef.current > 0) return; // more saves still queued → stay 'saving'
    if (!mountedRef.current) return;
    if (ok) {
      setSaveStatus('saved');
      if (savedTimerRef.current) clearTimeout(savedTimerRef.current);
      savedTimerRef.current = setTimeout(() => {
        if (mountedRef.current) setSaveStatus('idle');
      }, SAVED_LINGER_MS);
    } else {
      setSaveStatus('error');
    }
  }, []);

  /**
   * Serialized save of a field-value patch. Reads the current rowVersion at
   * execution time and replaces detail + rowVersion from the response.
   */
  const saveValues = useCallback(
    (patch: DocumentValuePatch): Promise<SaveResult> => {
      pendingRef.current += 1;
      if (mountedRef.current) setSaveStatus('saving');
      const run = chainRef.current.then(async (): Promise<SaveResult> => {
        const current = detailRef.current;
        if (!current) return { ok: false, message: 'No document loaded.' };
        // Once a conflict is latched, refuse further saves until reload so we
        // can't overwrite newer server state with stale local values. Read the
        // ref (not closure state) so same-batch queued saves see the latch.
        if (conflictRef.current) return { ok: false, conflict: true };
        try {
          const res = await saveValuesApi(instanceId, patch, current.rowVersion ?? undefined);
          if (res.success && res.data) {
            setDetailTree(res.data);
            return OK;
          }
          return { ok: false, errors: res.errors, message: res.message };
        } catch (err) {
          if (err instanceof AxiosError) {
            const status = err.response?.status;
            const body = err.response?.data as ApiResponse<unknown> | undefined;
            if (status === 409) {
              latchConflict(true);
              return { ok: false, conflict: true, message: body?.message };
            }
            return { ok: false, errors: body?.errors, message: body?.message };
          }
          return { ok: false, message: 'Something went wrong.' };
        }
      });
      chainRef.current = run.catch(() => undefined);
      void run.then((r) => settleStatus(r.ok)).catch(() => settleStatus(false));
      return run;
    },
    [instanceId, setDetailTree, settleStatus, latchConflict]
  );

  return {
    detail,
    isLoading,
    loadError,
    conflict,
    reloadKey,
    saveStatus,
    readOnly: detail !== null && detail.status !== 'Draft',
    reload,
    saveValues,
  };
}

export type DocumentInstance = ReturnType<typeof useDocumentInstance>;
