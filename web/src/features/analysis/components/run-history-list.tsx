import { Card } from "@/components/ui/card";
import { RunStatusBadge } from "./run-status-badge";
import type { AnalysisRun } from "../types";

interface RunHistoryListProps {
  runs: AnalysisRun[];
  isLoading: boolean;
  selectedRunId: number | null;
  onSelect: (runId: number) => void;
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

function sourceSummary(run: AnalysisRun): string {
  const count = run.sources.length;
  return `${count} source${count === 1 ? "" : "s"}`;
}

export function RunHistoryList({
  runs,
  isLoading,
  selectedRunId,
  onSelect,
}: RunHistoryListProps) {
  return (
    <Card data-testid="analysis-run-history">
      <h2 className="font-serif mb-4">Past Analyses</h2>

      {isLoading && runs.length === 0 ? (
        <p className="text-sm text-brand-slate-400">Loading…</p>
      ) : runs.length === 0 ? (
        <p className="text-sm text-brand-slate-400">No analyses yet.</p>
      ) : (
        <ul className="space-y-2">
          {runs.map((run) => {
            const isSelected = run.id === selectedRunId;
            return (
              <li key={run.id}>
                <button
                  type="button"
                  onClick={() => onSelect(run.id)}
                  data-testid={`analysis-run-row-${run.id}`}
                  className={`w-full text-left rounded-card border p-3 transition-colors ${
                    isSelected
                      ? "border-brand-teal-500 bg-brand-teal-50"
                      : "border-brand-slate-200 hover:border-brand-slate-300"
                  }`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-sm font-medium text-brand-slate-800">
                      {formatDateTime(run.createdAt)}
                    </span>
                    <RunStatusBadge status={run.status} />
                  </div>
                  <p className="text-xs text-brand-slate-400 mt-1">
                    {sourceSummary(run)}
                  </p>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}
