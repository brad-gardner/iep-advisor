import { useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useProgressReports } from "../hooks/use-progress-reports";
import { CreateProgressReportForm } from "./create-progress-report-form";
import { ProgressReportList } from "./progress-report-list";

interface ProgressReportsTabProps {
  iepId: number;
  childId: number;
  canEdit: boolean;
}

export function ProgressReportsTab({
  iepId,
  childId,
  canEdit,
}: ProgressReportsTabProps) {
  const [showCreate, setShowCreate] = useState(false);
  const { reports, isLoading, reload } = useProgressReports(iepId);

  return (
    <Card data-testid="progress-reports-section">
      <div className="flex justify-between items-center mb-4">
        <div>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800">
            Progress Reports
          </h2>
          <p className="text-sm text-brand-slate-500 mt-1">
            School-issued reports tracking progress against this IEP's goals.
          </p>
        </div>
        {canEdit && !showCreate && (
          <Button
            variant="secondary"
            onClick={() => setShowCreate(true)}
            data-testid="new-progress-report-button"
          >
            New Report
          </Button>
        )}
      </div>

      {canEdit && showCreate && (
        <div className="mb-4">
          <CreateProgressReportForm
            iepId={iepId}
            onCreated={() => {
              setShowCreate(false);
              reload();
            }}
            onCancel={() => setShowCreate(false)}
          />
        </div>
      )}

      <ProgressReportList
        reports={reports}
        isLoading={isLoading}
        childId={childId}
        iepId={iepId}
        canEdit={canEdit}
        onChanged={reload}
      />
    </Card>
  );
}
