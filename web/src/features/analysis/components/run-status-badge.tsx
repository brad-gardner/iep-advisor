import { Badge } from "@/components/ui/badge";
import type { AnalysisRunStatus } from "../types";

interface RunStatusBadgeProps {
  status: AnalysisRunStatus;
}

const STATUS_CONFIG: Record<
  AnalysisRunStatus,
  { label: string; variant: "neutral" | "warning" | "success" | "error" }
> = {
  Pending: { label: "Pending", variant: "neutral" },
  Running: { label: "Running", variant: "warning" },
  Completed: { label: "Completed", variant: "success" },
  Error: { label: "Error", variant: "error" },
};

export function RunStatusBadge({ status }: RunStatusBadgeProps) {
  const { label, variant } = STATUS_CONFIG[status] ?? STATUS_CONFIG.Pending;
  return <Badge variant={variant}>{label}</Badge>;
}
