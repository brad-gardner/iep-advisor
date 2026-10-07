import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { getActiveLanguage } from "@/lib/i18n/format";
import { RunStatusBadge } from "./run-status-badge";
import type { AnalysisRun } from "../types";

interface RunHistoryListProps {
  runs: AnalysisRun[];
  isLoading: boolean;
  selectedRunId: number | null;
  onSelect: (runId: number) => void;
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString(getActiveLanguage());
}

export function RunHistoryList({
  runs,
  isLoading,
  selectedRunId,
  onSelect,
}: RunHistoryListProps) {
  const { t } = useTranslation(["analysis", "common"]);
  return (
    <Card data-testid="analysis-run-history">
      <h2 className="font-serif mb-4">{t("historyList.heading")}</h2>

      {isLoading && runs.length === 0 ? (
        <p className="text-sm text-brand-slate-500">{t("common:ui.loading")}</p>
      ) : runs.length === 0 ? (
        <p className="text-sm text-brand-slate-500">{t("historyList.empty")}</p>
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
                  <p className="text-xs text-brand-slate-500 mt-1">
                    {t("historyList.sourceCount", { count: run.sources.length })}
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
