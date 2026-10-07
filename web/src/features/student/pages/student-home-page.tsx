import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PageLayout } from "@/components/ui/page-layout";
import { Spinner } from "@/components/ui/spinner";
import { Notice } from "@/components/ui/notice";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useToast } from "@/components/ui/toast";
import { usePageTitle } from "@/hooks/use-page-title";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { NextMeetingCard } from "@/features/home/components/next-meeting-card";
import { useHome } from "@/features/home/hooks/use-home";
import type { HomeMeetingDto } from "@/features/home/types";
import { AiInterviewHelper } from "../components/ai-interview-helper";
import { WorkspaceSection } from "../components/workspace-section";
import { useStudentWorkspace } from "../hooks/use-student-workspace";
import { getEntryKinds } from "../lib/entry-kinds";
import type {
  CreateWorkspaceEntryRequest,
  StudentWorkspaceEntryDto,
  StudentWorkspaceEntryKind,
} from "../types";

export function StudentHomePage() {
  const { t } = useTranslation(["student", "common"]);
  const { user } = useAuth();
  const { show } = useToast();
  const title = user?.firstName ? t("homePage.welcomeTitle", { name: user.firstName }) : t("homePage.defaultTitle");
  usePageTitle(title);
  const {
    entries,
    status,
    reload,
    addEntry,
    updateEntry,
    setShareable,
    removeEntry,
    interview,
  } = useStudentWorkspace();
  // Independent of the workspace fetch above — a failure here shouldn't block
  // the workspace, and vice versa. A failure renders its own error notice
  // below rather than silently looking like "nothing scheduled".
  const { home, error: homeError, retry: retryHome } = useHome();
  const studentHome = home?.kind === "Student" ? home.student : null;
  const [meetingOverride, setMeetingOverride] = useState<HomeMeetingDto | null>(null);
  const nextMeeting =
    meetingOverride && meetingOverride.id === studentHome?.nextMeeting?.id
      ? meetingOverride
      : studentHome?.nextMeeting ?? null;

  const entriesByKind = (
    kind: StudentWorkspaceEntryKind,
  ): StudentWorkspaceEntryDto[] =>
    entries
      .filter((e) => e.entryKind === kind)
      .sort((a, b) => a.displayOrder - b.displayOrder);

  // AI interview answers live alongside meeting statements in the UI.
  const meetingEntries = [
    ...entriesByKind("MeetingStatement"),
    ...entriesByKind("AiInterviewAnswer"),
  ].sort((a, b) => a.displayOrder - b.displayOrder);

  // Mutations confirm success with a toast; the hook keeps local state in sync.
  // Click-triggered (never a mount effect), so translating inline here is
  // safe — no stale-`t`-in-a-fetch-dependency-array concern (see `useHome`'s
  // comment for the case where that WOULD matter).
  const handleAdd = async (
    input: CreateWorkspaceEntryRequest,
  ): Promise<boolean> => {
    const ok = await addEntry(input);
    if (ok) show({ message: t("homePage.addedToast"), variant: "success" });
    return ok;
  };

  const handleUpdate = async (
    id: number,
    content: string,
    isShareable: boolean,
  ): Promise<boolean> => {
    const ok = await updateEntry(id, content, isShareable);
    if (ok) show({ message: t("homePage.savedToast"), variant: "success" });
    return ok;
  };

  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    setIsDeleting(true);
    const ok = await removeEntry(pendingDeleteId);
    if (ok) show({ message: t("homePage.deletedToast"), variant: "success" });
    setIsDeleting(false);
    setPendingDeleteId(null);
  };

  return (
    <PageLayout
      data-testid="student-home"
      title={title}
      subtitle={t("homePage.subtitle")}
    >
      {homeError && (
        <div role="alert">
          <Notice variant="error" title={homeError} data-testid="student-home-error">
            <Button variant="secondary" size="sm" className="mt-2" onClick={retryHome} data-testid="student-home-retry">
              {t("common:ui.tryAgain")}
            </Button>
          </Notice>
        </div>
      )}

      {studentHome?.workspaceNudge && (
        <Notice variant="info" title={studentHome.workspaceNudge} data-testid="student-home-nudge" />
      )}

      {nextMeeting && (
        <NextMeetingCard
          meeting={nextMeeting}
          onUpdated={setMeetingOverride}
          data-testid="student-home-next-meeting"
        />
      )}

      {status === "loading" && (
        <div className="flex justify-center py-12">
          <Spinner
            data-testid="student-workspace-loading"
            label={t("homePage.loadingWorkspace")}
          />
        </div>
      )}

      {status === "error" && (
        <Notice variant="error" title={t("homePage.workspaceLoadErrorTitle")}>
          <p>{t("common:ui.genericError")}</p>
          <div className="mt-3">
            <Button variant="secondary" size="sm" onClick={() => void reload()}>
              {t("common:ui.tryAgain")}
            </Button>
          </div>
        </Notice>
      )}

      {status === "ready" && (
        <div className="space-y-8" data-testid="student-workspace">
          {getEntryKinds().map((meta) => (
            <WorkspaceSection
              key={meta.kind}
              meta={meta}
              entries={
                meta.kind === "MeetingStatement"
                  ? meetingEntries
                  : entriesByKind(meta.kind)
              }
              onAdd={(content, isShareable) =>
                handleAdd({ entryKind: meta.kind, content, isShareable })
              }
              onUpdate={handleUpdate}
              onSetShareable={setShareable}
              onDelete={setPendingDeleteId}
            />
          ))}

          <AiInterviewHelper
            onInterview={interview}
            onSave={(content, entryKind) =>
              handleAdd({ entryKind, content, isShareable: false })
            }
          />
        </div>
      )}

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title={t("homePage.deleteDialogTitle")}
        message={t("homePage.deleteDialogMessage")}
        confirmLabel={t("homePage.deleteDialogTitle")}
        loading={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="student-entry-delete-dialog"
      />
    </PageLayout>
  );
}
