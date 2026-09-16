import { useCallback, useEffect, useRef, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { listAuditIntegrityRuns, runAuditIntegrityCheck } from '../api/audit-admin-api';
import type { AuditIntegrityRunDto } from '../types';

interface UseAuditIntegrityResult {
  runs: AuditIntegrityRunDto[];
  isLoading: boolean;
  error: string | null;
  reload: () => void;
  runCheck: () => Promise<void>;
  isRunning: boolean;
  runError: string | null;
}

/** Platform-admin `/admin/audit`: the last integrity runs plus a manual
 *  "Run check now" action. No polling — a run is synchronous (the nightly
 *  `AuditIntegrityWorker` covers the background case) — but still guards
 *  against a stale response landing after a newer load/run, the same
 *  "latest wins" shape used by `useDistrictExports`. */
export function useAuditIntegrity(): UseAuditIntegrityResult {
  const [runs, setRuns] = useState<AuditIntegrityRunDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [runError, setRunError] = useState<string | null>(null);
  const reloadRef = useRef<() => void>(() => {});

  useEffect(() => {
    let active = true;
    let generation = 0;

    async function load() {
      const mine = ++generation;
      try {
        const res = await listAuditIntegrityRuns();
        if (!active || mine !== generation) return;
        if (res.success && res.data) {
          setRuns(res.data);
          setError(null);
        } else {
          setError(res.message ?? 'Could not load integrity runs.');
        }
      } catch (err) {
        if (active && mine === generation) setError(apiErrorMessage(err, 'Could not load integrity runs.'));
      } finally {
        if (active && mine === generation) setIsLoading(false);
      }
    }

    reloadRef.current = () => void load();
    void load();

    return () => {
      active = false;
    };
  }, []);

  const runCheck = useCallback(async () => {
    setIsRunning(true);
    setRunError(null);
    try {
      const res = await runAuditIntegrityCheck();
      if (res.success) {
        reloadRef.current();
      } else {
        setRunError(res.message ?? 'Could not run the integrity check.');
      }
    } catch (err) {
      setRunError(apiErrorMessage(err, 'Could not run the integrity check.'));
    } finally {
      setIsRunning(false);
    }
  }, []);

  return {
    runs,
    isLoading,
    error,
    reload: () => reloadRef.current(),
    runCheck,
    isRunning,
    runError,
  };
}
