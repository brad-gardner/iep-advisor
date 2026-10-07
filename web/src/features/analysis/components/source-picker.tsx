import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
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
  // `etr-documents` and `common` aren't `SourcePicker`'s own namespace keys —
  // included so their Spanish data is loaded before `etrLabel`/`iepLabel`
  // (plain functions, not this hook's `t`) call `evaluationTypeLabel`/
  // `documentMeetingTypeLabel` from render (see docs/i18n/README.md).
  const { t } = useTranslation(["analysis", "etr-documents", "common"]);
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
      <h2 className="font-serif mb-1">{t("sourcePicker.heading")}</h2>
      <p className="text-sm text-brand-slate-500 mb-4">
        {t("sourcePicker.body")}
      </p>

      {isLoading ? (
        <p className="text-sm text-brand-slate-500">{t("sourcePicker.loading")}</p>
      ) : !hasAnySource ? (
        <p className="text-sm text-brand-slate-500">
          {t("sourcePicker.empty")}
        </p>
      ) : (
        <div className="space-y-4">
          <SourceCheckboxGroup
            title={t("sourcePicker.iepsGroup")}
            options={iepOptions}
            selected={selectedKeys}
            onToggle={toggle}
          />
          <SourceCheckboxGroup
            title={t("sourcePicker.etrsGroup")}
            options={etrOptions}
            selected={selectedKeys}
            onToggle={toggle}
          />
          <SourceCheckboxGroup
            title={t("sourcePicker.progressReportsGroup")}
            options={progressReportOptions}
            selected={selectedKeys}
            onToggle={toggle}
          />
        </div>
      )}

      <div className="mt-5">
        <Button
          onClick={handleRun}
          loading={isRunning}
          disabled={selected.size === 0}
          data-testid="run-analysis-button"
        >
          {t("sourcePicker.run")}
        </Button>
      </div>
    </Card>
  );
}
