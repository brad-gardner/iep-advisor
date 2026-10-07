import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage, loadErrorText, toLoadError, type LoadError } from '@/lib/api-error';
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
  const { t } = useTranslation('admin');
  const [runs, setRuns] = useState<AuditIntegrityRunDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // A flag (shared `LoadError` shape), not pre-translated text — translated
  // at render, below, so a language switch after a failed load shows the new
  // language immediately rather than a stale snapshot (see
  // `docs/i18n/README.md`'s "Load errors: the shared `LoadError` pattern").
  const [loadError, setLoadError] = useState<LoadError | null>(null);
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
          setLoadError(null);
        } else {
          setLoadError(toLoadError(res));
        }
      } catch (err) {
        if (active && mine === generation) setLoadError(toLoadError(err));
      } finally {
        if (active && mine === generation) setIsLoading(false);
      }
    }

    reloadRef.current = () => void load();
    void load();

    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `loadError` comment above.
  }, []);

  const error = loadErrorText(loadError, t('audit.loadFailed'));

  const runCheck = useCallback(async () => {
    setIsRunning(true);
    setRunError(null);
    try {
      const res = await runAuditIntegrityCheck();
      if (res.success) {
        reloadRef.current();
      } else {
        setRunError(res.message ?? t('audit.runFailed'));
      }
    } catch (err) {
      setRunError(apiErrorMessage(err, t('audit.runFailed')));
    } finally {
      setIsRunning(false);
    }
  }, [t]);

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
