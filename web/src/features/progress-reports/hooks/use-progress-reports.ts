import { useCallback, useEffect, useState } from "react";
import { listByIep } from "../api/progress-reports-api";
import type { ProgressReport } from "../types";

export function useProgressReports(iepId: number) {
  const [reports, setReports] = useState<ProgressReport[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(async () => {
    if (!iepId) return;
    setIsLoading(true);
    try {
      const res = await listByIep(iepId);
      if (res.success && res.data) {
        setReports(res.data);
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsLoading(false);
    }
  }, [iepId]);

  useEffect(() => {
    reload();
  }, [reload]);

  return { reports, isLoading, reload };
}
