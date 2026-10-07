import { useEffect, useState } from "react";
import { useParams, useNavigate, Link, Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { UserX } from "lucide-react";
import type { ChildProfile, CreateChildProfileRequest } from "@/types/api";
import { getChild, updateChild, deleteChild } from "../api/children-api";
import { ChildForm } from "./child-form";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import { SchoolLinkBadge } from "@/features/child-links/components/school-link-badge";
import { getChildSchoolLinks } from "@/features/child-links/api/child-links-api";
import type { ChildSchoolLink } from "@/features/child-links/types";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { PageLayout } from "@/components/ui/page-layout";
import { useToast } from "@/components/ui/toast";
import { TabsNav, TabLink } from "@/components/ui/tabs";
import { usePageTitle } from "@/hooks/use-page-title";

export function ChildDetailPage() {
  const { t } = useTranslation("children");
  const { childId: childIdParam } = useParams<{ childId: string }>();
  const childId = Number(childIdParam);
  const navigate = useNavigate();
  const { show: showToast } = useToast();
  const [child, setChild] = useState<ChildProfile | null>(null);
  // Never the child's legal name — the tab title lands in browser history,
  // OS taskbar/Alt-Tab previews, and screen-share tab pickers, all reachable
  // by a bystander who never authenticated to the app. The full name stays in
  // the in-page heading only (see `PageLayout title=...` below).
  usePageTitle(t("detail.pageTitle"));
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isConfirmingRemove, setIsConfirmingRemove] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [schoolLinks, setSchoolLinks] = useState<ChildSchoolLink[]>([]);

  const reloadChild = async () => {
    const response = await getChild(childId);
    if (response.success && response.data) {
      setChild(response.data);
    }
  };

  useEffect(() => {
    async function load() {
      try {
        await reloadChild();
      } catch {
        // handled by interceptor
      } finally {
        setIsLoading(false);
      }
    }
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [childId]);

  useEffect(() => {
    if (!childId) return;
    let active = true;
    getChildSchoolLinks(childId)
      .then((res) => {
        if (active && res.success && res.data) {
          setSchoolLinks(res.data);
        }
      })
      .catch(() => {
        // Non-critical: the badge simply won't render.
      });
    return () => {
      active = false;
    };
  }, [childId]);

  const handleUpdate = async (data: CreateChildProfileRequest) => {
    try {
      const response = await updateChild(childId, data);
      if (response.success) {
        const refreshed = await getChild(childId);
        if (refreshed.success && refreshed.data) {
          setChild(refreshed.data);
        }
        setIsEditing(false);
        showToast({ message: t("detail.toastSaved"), variant: "success" });
        return { success: true };
      }
      return { success: false, error: response.message || t("detail.updateFailed") };
    } catch {
      return { success: false, error: t("errors.generic") };
    }
  };

  const handleDelete = async () => {
    setIsDeleting(true);
    try {
      const response = await deleteChild(childId);
      if (response.success) {
        showToast({ message: t("detail.toastRemoved"), variant: "success" });
        navigate("/children");
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsDeleting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t("detail.loadingChild")} />
      </div>
    );
  }

  if (!child) {
    return (
      <EmptyState
        icon={UserX}
        title={t("detail.notFoundTitle")}
        action={
          <Link to="/children">
            <Button variant="secondary">{t("detail.backToChildren")}</Button>
          </Link>
        }
      />
    );
  }

  const isOwner = child.role === "owner";
  const base = `/children/${childId}`;

  return (
    <PageLayout
      title={`${child.firstName} ${child.lastName ?? ""}`.trim()}
      breadcrumb={[
        { label: t("detail.breadcrumbChildren"), to: "/children" },
        { label: `${child.firstName} ${child.lastName ?? ""}`.trim() },
      ]}
      actions={
        isOwner ? (
          <>
            <Button
              variant="secondary"
              onClick={() => setIsEditing(true)}
              data-testid="child-edit-button"
            >
              {t("detail.edit")}
            </Button>
            <Button
              variant="danger"
              onClick={() => setIsConfirmingRemove(true)}
              data-testid="child-remove-button"
            >
              {t("detail.remove")}
            </Button>
          </>
        ) : undefined
      }
    >
      {(child.role !== "owner" || schoolLinks.length > 0) && (
        <div className="flex flex-wrap items-center gap-2">
          {!isOwner && <SharedBadge role={child.role} />}
          <SchoolLinkBadge links={schoolLinks} />
        </div>
      )}

      <TabsNav>
        <TabLink to={`${base}/overview`} testId="tab-overview">
          {t("detail.tabOverview")}
        </TabLink>
        <TabLink to={`${base}/goals`} testId="tab-goals">
          {t("detail.tabGoals")}
        </TabLink>
        <TabLink to={`${base}/analysis`} testId="tab-analysis">
          {t("detail.tabAnalysis")}
        </TabLink>
        <TabLink to={`${base}/meeting-prep`} testId="tab-meeting-prep">
          {t("detail.tabMeetingPrep")}
        </TabLink>
        <TabLink to={`${base}/ieps`} testId="tab-ieps">
          {t("detail.tabIeps")}
        </TabLink>
        <TabLink to={`${base}/etrs`} testId="tab-etrs">
          {t("detail.tabEtrs")}
        </TabLink>
        <TabLink to={`${base}/journal`} testId="tab-journal">
          {t("detail.tabJournal")}
        </TabLink>
        <TabLink to={`${base}/advocate`} testId="tab-advocate">
          {t("detail.tabAdvocate")}
        </TabLink>
      </TabsNav>

      <Outlet
        context={{ child, childId, reloadChild } satisfies ChildOutletContext}
      />

      <Modal
        open={isEditing}
        onClose={() => setIsEditing(false)}
        title={t("detail.editModalTitle", { name: child.firstName })}
        data-testid="child-edit-modal"
      >
        <ChildForm
          embedded
          initialValues={{
            firstName: child.firstName,
            lastName: child.lastName ?? "",
            dateOfBirth: child.dateOfBirth?.split("T")[0] ?? "",
            gradeLevel: child.gradeLevel ?? "",
            disabilityCategory: child.disabilityCategory ?? "",
            schoolDistrict: child.schoolDistrict ?? "",
          }}
          onSubmit={handleUpdate}
          submitLabel={t("detail.saveChanges")}
        />
      </Modal>

      <ConfirmDialog
        open={isConfirmingRemove}
        title={t("detail.removeDialogTitle")}
        message={t("detail.removeDialogMessage", { name: child.firstName })}
        confirmLabel={t("detail.removeConfirm")}
        loading={isDeleting}
        onConfirm={handleDelete}
        onCancel={() => setIsConfirmingRemove(false)}
        data-testid="child-remove-dialog"
      />
    </PageLayout>
  );
}

export interface ChildOutletContext {
  child: ChildProfile;
  childId: number;
  reloadChild: () => Promise<void>;
}
