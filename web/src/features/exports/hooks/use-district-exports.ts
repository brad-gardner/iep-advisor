import { useCallback, useEffect, useRef, useState } from 'react';
import { toLoadError, type LoadError } from '@/lib/api-error';
import { enqueueDistrictExport, listDistrictExports } from '../api/exports-api';
import { IN_FLIGHT_EXPORT_STATUSES } from '../types';
import type { ExportJobDto } from '../types';

const POLL_INTERVAL_MS = 10_000;

/** The shared `LoadError` shape, or `null` for "no error" — see
 * `docs/i18n/README.md`'s "Load errors: the shared `LoadError` pattern". A
 * server-provided message is shown as-is; the generic case is translated by
 * the caller at render time (it has the current `t`). */
export type ExportsLoadError = LoadError | null;

interface UseDistrictExportsResult {
  jobs: ExportJobDto[];
  isLoading: boolean;
  error: ExportsLoadError;
  retry: () => void;
  requestExport: () => Promise<void>;
  isRequesting: boolean;
  requestError: ExportsLoadError;
}

/** District export jobs (district- and student-scoped): loads once, then
 *  polls every 10s while any job is Queued/Running, and does nothing once
 *  none are (plan 7, decision 8).
 *
 * The fetch is declared inside the one effect (mirrors
 * `NotificationsProvider`'s unread-count poll) so the interval can read the
 * latest jobs via a ref without re-subscribing on every list change, and
 * `reload()` (used by `retry`/`requestExport`) is a stable function backed by
 * that same ref.
 */
export function useDistrictExports(): UseDistrictExportsResult {
  const [jobs, setJobs] = useState<ExportJobDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<ExportsLoadError>(null);
  const [isRequesting, setIsRequesting] = useState(false);
  const [requestError, setRequestError] = useState<ExportsLoadError>(null);
  const jobsRef = useRef<ExportJobDto[]>([]);
  const reloadRef = useRef<() => void>(() => {});

  useEffect(() => {
    let active = true;
    // Latest request wins: a slow poll that resolves after a newer one (or after a manual reload)
    // must not roll the table back to an older snapshot.
    let generation = 0;

    async function load() {
      const mine = ++generation;
      try {
        const res = await listDistrictExports();
        if (!active || mine !== generation) return;
        if (res.success && res.data) {
          jobsRef.current = res.data;
          setJobs(res.data);
          setError(null);
        } else {
          setError(toLoadError(res));
        }
      } catch (err) {
        if (active && mine === generation) setError(toLoadError(err));
      } finally {
        if (active && mine === generation) setIsLoading(false);
      }
    }

    reloadRef.current = () => void load();

    void load();
    const interval = setInterval(() => {
      if (jobsRef.current.some((j) => IN_FLIGHT_EXPORT_STATUSES.has(j.status))) {
        void load();
      }
    }, POLL_INTERVAL_MS);

    return () => {
      active = false;
      clearInterval(interval);
    };
  }, []);

  const requestExport = useCallback(async () => {
    setIsRequesting(true);
    setRequestError(null);
    try {
      const res = await enqueueDistrictExport();
      if (res.success) {
        reloadRef.current();
      } else {
        setRequestError(toLoadError(res));
      }
    } catch (err) {
      setRequestError(toLoadError(err));
    } finally {
      setIsRequesting(false);
    }
  }, []);

  return {
    jobs,
    isLoading,
    error,
    retry: () => reloadRef.current(),
    requestExport,
    isRequesting,
    requestError,
  };
}
