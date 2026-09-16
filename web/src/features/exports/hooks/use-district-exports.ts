import { useCallback, useEffect, useRef, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { enqueueDistrictExport, listDistrictExports } from '../api/exports-api';
import { IN_FLIGHT_EXPORT_STATUSES } from '../types';
import type { ExportJobDto } from '../types';

const POLL_INTERVAL_MS = 10_000;

interface UseDistrictExportsResult {
  jobs: ExportJobDto[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  requestExport: () => Promise<void>;
  isRequesting: boolean;
  requestError: string | null;
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
  const [error, setError] = useState<string | null>(null);
  const [isRequesting, setIsRequesting] = useState(false);
  const [requestError, setRequestError] = useState<string | null>(null);
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
          setError(res.message ?? 'Could not load export jobs.');
        }
      } catch (err) {
        if (active && mine === generation) setError(apiErrorMessage(err, 'Could not load export jobs.'));
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
        setRequestError(res.message ?? 'Could not request the export.');
      }
    } catch (err) {
      setRequestError(apiErrorMessage(err, 'Could not request the export.'));
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
