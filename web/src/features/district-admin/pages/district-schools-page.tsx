import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, Pencil, Ban, School } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { PageLayout } from "@/components/ui/page-layout";
import { Table, type TableColumn } from "@/components/ui/table";
import { useToast } from "@/components/ui/toast";
import { usePageTitle } from "@/hooks/use-page-title";
import { reloadEducatorProfile } from "@/features/educator/hooks/use-educator-profile";
import {
  createSchool,
  deactivateSchool,
  getDistrictSchools,
  updateSchool,
} from "../api/district-api";
import { FamilyDraftSharingToggle } from "../components/family-draft-sharing-toggle";
import { SchoolForm } from "../components/school-form";
import type { DistrictSchool, SaveSchoolRequest } from "../types";

export function DistrictSchoolsPage() {
  const { t } = useTranslation('district-admin');
  usePageTitle(t('schoolsPage.title'));
  const { show: showToast } = useToast();
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editing, setEditing] = useState<DistrictSchool | null>(null);
  const [deactivating, setDeactivating] = useState<DistrictSchool | null>(null);
  const [isDeactivating, setIsDeactivating] = useState(false);
  const [deactivateError, setDeactivateError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    try {
      const response = await getDistrictSchools();
      setSchools(response.success && response.data ? response.data : []);
    } catch {
      setSchools([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  const handleCreate = async (data: SaveSchoolRequest) => {
    try {
      const response = await createSchool(data);
      if (response.success) {
        await reload();
        // School counts in the district overview are now stale.
        void reloadEducatorProfile();
        setIsAddOpen(false);
        showToast({ message: t('schoolsPage.toasts.created'), variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || t('schoolsPage.errors.addFailed'),
      };
    } catch {
      return { success: false, error: t('schoolsPage.errors.generic') };
    }
  };

  const handleUpdate = async (data: SaveSchoolRequest) => {
    if (!editing) return { success: false, error: t('schoolsPage.errors.noSchoolSelected') };
    try {
      const response = await updateSchool(editing.id, data);
      if (response.success) {
        await reload();
        setEditing(null);
        showToast({ message: t('schoolsPage.toasts.updated'), variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || t('schoolsPage.errors.updateFailed'),
      };
    } catch {
      return { success: false, error: t('schoolsPage.errors.generic') };
    }
  };

  const confirmDeactivate = async () => {
    if (!deactivating) return;
    setIsDeactivating(true);
    setDeactivateError(null);
    try {
      const response = await deactivateSchool(deactivating.id);
      if (response.success) {
        await reload();
        void reloadEducatorProfile();
        setDeactivating(null);
        showToast({ message: t('schoolsPage.toasts.deactivated'), variant: "success" });
      } else {
        // The backend returns an explicit message when a school still has
        // active students or staff — surface it verbatim, inside the dialog.
        setDeactivateError(
          response.message || t('schoolsPage.errors.cannotDeactivate'),
        );
      }
    } catch {
      setDeactivateError(t('schoolsPage.errors.generic'));
    } finally {
      setIsDeactivating(false);
    }
  };

  const columns: TableColumn<DistrictSchool>[] = [
    {
      key: "name",
      header: t('schoolsPage.columns.school'),
      cell: (s) => (
        <span className="font-medium text-brand-slate-800">{s.name}</span>
      ),
      sortValue: (s) => s.name,
    },
    {
      key: "state",
      header: t('schoolsPage.columns.state'),
      hideBelow: "md",
      cell: (s) => s.stateCode || "—",
      sortValue: (s) => s.stateCode || "",
    },
    {
      key: "students",
      header: t('schoolsPage.columns.students'),
      align: "right",
      cell: (s) => s.activeStudentCount,
      sortValue: (s) => s.activeStudentCount,
    },
    {
      key: "staff",
      header: t('schoolsPage.columns.staff'),
      align: "right",
      hideBelow: "md",
      cell: (s) => s.activeStaffCount,
      sortValue: (s) => s.activeStaffCount,
    },
  ];

  return (
    <PageLayout
      title={t('schoolsPage.title')}
      data-testid="district-schools-page"
      actions={
        <Button
          onClick={() => setIsAddOpen(true)}
          data-testid="district-schools-add"
        >
          <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          {t('schoolsPage.addSchool')}
        </Button>
      }
    >
      <div className="mb-6">
        <FamilyDraftSharingToggle />
      </div>

      <Table
        label={t('schoolsPage.title')}
        data-testid="district-schools-table"
        columns={columns}
        rows={schools}
        rowKey={(s) => s.id}
        loading={isLoading}
        defaultSort={{ key: "name", direction: "asc" }}
        rowActionLabel={(s) => s.name}
        rowActions={(s) => [
          {
            label: t('schoolsPage.rowActions.edit'),
            icon: <Pencil className="h-3.5 w-3.5" strokeWidth={1.8} />,
            onSelect: () => setEditing(s),
            "data-testid": `district-school-edit-${s.id}`,
          },
          {
            label: t('schoolsPage.rowActions.deactivate'),
            icon: <Ban className="h-3.5 w-3.5" strokeWidth={1.8} />,
            variant: "danger",
            onSelect: () => {
              setDeactivateError(null);
              setDeactivating(s);
            },
            "data-testid": `district-school-deactivate-${s.id}`,
          },
        ]}
        empty={
          <EmptyState
            data-testid="district-schools-empty"
            icon={School}
            title={t('schoolsPage.emptyTitle')}
            description={t('schoolsPage.emptyDescription')}
          />
        }
      />

      <Modal
        open={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        title={t('schoolsPage.addModalTitle')}
        data-testid="district-schools-add-modal"
      >
        <SchoolForm
          mode="create"
          submitLabel={t('schoolsPage.addSchool')}
          onSubmit={handleCreate}
          onCancel={() => setIsAddOpen(false)}
          testIdPrefix="district-schools-create"
        />
      </Modal>

      <Modal
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={t('schoolsPage.editModalTitle')}
        data-testid="district-schools-edit-modal"
      >
        {editing && (
          <SchoolForm
            mode="edit"
            initialName={editing.name}
            initialStateCode={editing.stateCode ?? ""}
            submitLabel={t('schoolsPage.editSubmit')}
            onSubmit={handleUpdate}
            onCancel={() => setEditing(null)}
            testIdPrefix={`district-school-edit-form-${editing.id}`}
          />
        )}
      </Modal>

      <ConfirmDialog
        open={deactivating !== null}
        title={t('schoolsPage.deactivateDialogTitle')}
        message={
          deactivating
            ? t('schoolsPage.deactivateMessage', { name: deactivating.name })
            : ""
        }
        confirmLabel={t('schoolsPage.deactivateConfirmLabel')}
        loading={isDeactivating}
        error={deactivateError}
        onConfirm={confirmDeactivate}
        onCancel={() => setDeactivating(null)}
        data-testid="district-school-deactivate-dialog"
      />
    </PageLayout>
  );
}
