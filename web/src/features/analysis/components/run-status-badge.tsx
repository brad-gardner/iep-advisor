import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import type { AnalysisRunStatus } from "../types";

interface RunStatusBadgeProps {
  status: AnalysisRunStatus;
}

export function RunStatusBadge({ status }: RunStatusBadgeProps) {
  const { t } = useTranslation("analysis");
  const STATUS_CONFIG: Record<
    AnalysisRunStatus,
    { label: string; variant: "neutral" | "warning" | "success" | "error" }
  > = {
    Pending: { label: t("runStatus.pending"), variant: "neutral" },
    Running: { label: t("runStatus.running"), variant: "warning" },
    Completed: { label: t("runStatus.completed"), variant: "success" },
    Error: { label: t("runStatus.error"), variant: "error" },
  };
  const { label, variant } = STATUS_CONFIG[status] ?? STATUS_CONFIG.Pending;
  return <Badge variant={variant}>{label}</Badge>;
}
