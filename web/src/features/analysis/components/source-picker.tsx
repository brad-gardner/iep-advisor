import { useMemo, useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useAnalysisSources } from "../hooks/use-analysis-sources";
import {
  etrLabel,
  iepLabel,
  progressReportLabel,
} from "./source-labels";
import { SourceCheckboxGroup } from "./source-checkbox-group";
import {
  sourceKey,
  type CreateAnalysisRunRequest,
  type SourceOption,
} from "../types";

interface SourcePickerProps {
  childId: number;
  isRunning: boolean;
  onRun: (payload: CreateAnalysisRunRequest) => void;
}

export function SourcePicker({ childId, isRunning, onRun }: SourcePickerProps) {
  const { sources, isLoading } = useAnalysisSources(childId);
  const [selected, setSelected] = useState<Map<string, SourceOption>>(
    new Map()
  );

  const iepOptions: SourceOption[] = useMemo(
    () =>
      sources.ieps.map((iep) => ({
        sourceType: "IepDocument",
        sourceId: iep.id,
        label: iepLabel(iep),
      })),
    [sources.ieps]
  );

  const etrOptions: SourceOption[] = useMemo(
    () =>
      sources.etrs.map((etr) => ({
        sourceType: "EtrDocument",
        sourceId: etr.id,
        label: etrLabel(etr),
      })),
    [sources.etrs]
  );

  const progressReportOptions: SourceOption[] = useMemo(
    () =>
      sources.progressReports.map((report) => ({
        sourceType: "ProgressReport",
        sourceId: report.id,
        label: progressReportLabel(report),
      })),
    [sources.progressReports]
  );

  const selectedKeys = useMemo(
    () => new Set(selected.keys()),
    [selected]
  );

  const toggle = (option: SourceOption) => {
    const key = sourceKey(option.sourceType, option.sourceId);
    setSelected((prev) => {
      const next = new Map(prev);
      if (next.has(key)) next.delete(key);
      else next.set(key, option);
      return next;
    });
  };

  const handleRun = () => {
    const chosen = Array.from(selected.values());
    onRun({
      sources: chosen.map((o) => ({
        sourceType: o.sourceType,
        sourceId: o.sourceId,
      })),
    });
  };

  const hasAnySource =
    iepOptions.length > 0 ||
    etrOptions.length > 0 ||
    progressReportOptions.length > 0;

  return (
    <Card data-testid="analysis-source-picker">
      <h2 className="font-serif mb-1">New Analysis</h2>
      <p className="text-sm text-brand-slate-400 mb-4">
        Select one or more documents to analyze together.
      </p>

      {isLoading ? (
        <p className="text-sm text-brand-slate-400">Loading documents…</p>
      ) : !hasAnySource ? (
        <p className="text-sm text-brand-slate-400">
          No documents available yet. Add an IEP, ETR, or progress report first.
        </p>
      ) : (
        <div className="space-y-4">
          <SourceCheckboxGroup
            title="IEPs"
            options={iepOptions}
            selected={selectedKeys}
            onToggle={toggle}
          />
          <SourceCheckboxGroup
            title="ETRs"
            options={etrOptions}
            selected={selectedKeys}
            onToggle={toggle}
          />
          <SourceCheckboxGroup
            title="Progress Reports"
            options={progressReportOptions}
            selected={selectedKeys}
            onToggle={toggle}
          />
        </div>
      )}

      <div className="mt-5">
        <Button
          onClick={handleRun}
          disabled={selected.size === 0 || isRunning}
          data-testid="run-analysis-button"
        >
          {isRunning ? "Starting…" : "Run analysis"}
        </Button>
      </div>
    </Card>
  );
}
