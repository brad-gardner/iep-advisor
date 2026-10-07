import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
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
  const { t } = useTranslation("progress-reports");
  const [showCreate, setShowCreate] = useState(false);
  const { reports, isLoading, reload } = useProgressReports(iepId);

  return (
    <Card data-testid="progress-reports-section">
      <div className="flex justify-between items-center mb-4">
        <div>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800">
            {t("tab.heading")}
          </h2>
          <p className="text-sm text-brand-slate-500 mt-1">
            {t("tab.body")}
          </p>
        </div>
        {canEdit && (
          <Button
            variant="secondary"
            onClick={() => setShowCreate(true)}
            data-testid="new-progress-report-button"
          >
            {t("tab.newReport")}
          </Button>
        )}
      </div>

      <ProgressReportList
        reports={reports}
        isLoading={isLoading}
        childId={childId}
        iepId={iepId}
        canEdit={canEdit}
        onChanged={reload}
      />

      <Modal
        open={showCreate}
        onClose={() => setShowCreate(false)}
        title={t("tab.newReportModalTitle")}
        data-testid="new-progress-report-modal"
      >
        <CreateProgressReportForm
          iepId={iepId}
          onCreated={() => {
            setShowCreate(false);
            reload();
          }}
          onCancel={() => setShowCreate(false)}
        />
      </Modal>
    </Card>
  );
}
