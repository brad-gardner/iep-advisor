import { useCallback, useEffect, useState } from "react";
import { getIepDocuments } from "@/features/iep-documents/api/iep-documents-api";
import { listByChild as listEtrsByChild } from "@/features/etr-documents/api/etr-documents-api";
import { listByIep as listProgressReportsByIep } from "@/features/progress-reports/api/progress-reports-api";
import type { IepDocument } from "@/types/api";
import type { EtrDocument } from "@/features/etr-documents/types";
import type { ProgressReport } from "@/features/progress-reports/types";

interface AnalysisSources {
  ieps: IepDocument[];
  etrs: EtrDocument[];
  progressReports: ProgressReport[];
}

const EMPTY: AnalysisSources = { ieps: [], etrs: [], progressReports: [] };

// Loads the child's analyzable source documents: IEPs, ETRs, and the progress
// reports attached to each of those IEPs.
export function useAnalysisSources(childId: number) {
  const [sources, setSources] = useState<AnalysisSources>(EMPTY);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(async () => {
    if (!childId) return;
    setIsLoading(true);
    try {
      const [iepRes, etrRes] = await Promise.all([
        getIepDocuments(childId),
        listEtrsByChild(childId),
      ]);

      const ieps = iepRes.success && iepRes.data ? iepRes.data : [];
      const etrs = etrRes.success && etrRes.data ? etrRes.data : [];

      const prResults = await Promise.all(
        ieps.map((iep) => listProgressReportsByIep(iep.id))
      );
      const progressReports = prResults.flatMap((res) =>
        res.success && res.data ? res.data : []
      );

      setSources({ ieps, etrs, progressReports });
    } catch {
      setSources(EMPTY);
    } finally {
      setIsLoading(false);
    }
  }, [childId]);

  useEffect(() => {
    reload();
  }, [reload]);

  return { sources, isLoading, reload };
}
