import { useCallback, useEffect, useState } from "react";
import { usePolling } from "@/hooks/use-polling";
import { listByIep } from "../api/progress-reports-api";
import type { ProgressReport } from "../types";

const IN_FLIGHT_STATUSES = new Set(["uploaded", "processing"]);

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

  // Quietly refresh while any report is mid-analysis so the status pill updates.
  const refreshInBackground = useCallback(async () => {
    if (!iepId) return;
    try {
      const res = await listByIep(iepId);
      if (res.success && res.data) setReports(res.data);
    } catch {
      // ignore during polling
    }
  }, [iepId]);

  const hasInFlight = reports.some((r) => IN_FLIGHT_STATUSES.has(r.status));
  usePolling(refreshInBackground, 4000, hasInFlight);

  return { reports, isLoading, reload };
}
