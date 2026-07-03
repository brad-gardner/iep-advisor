import { useCallback, useEffect, useState } from "react";
import { Plus, Pencil, Ban, School } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { PageLayout } from "@/components/ui/page-layout";
import { Table, type TableColumn } from "@/components/ui/table";
import { useToast } from "@/components/ui/toast";
import { reloadEducatorProfile } from "@/features/educator/hooks/use-educator-profile";
import {
  createSchool,
  deactivateSchool,
  getDistrictSchools,
  updateSchool,
} from "../api/district-api";
import { SchoolForm } from "../components/school-form";
import type { DistrictSchool, SaveSchoolRequest } from "../types";

export function DistrictSchoolsPage() {
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
        showToast({ message: "School created", variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || "Failed to add school",
      };
    } catch {
      return { success: false, error: "An error occurred" };
    }
  };

  const handleUpdate = async (data: SaveSchoolRequest) => {
    if (!editing) return { success: false, error: "No school selected" };
    try {
      const response = await updateSchool(editing.id, data);
      if (response.success) {
        await reload();
        setEditing(null);
        showToast({ message: "School updated", variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || "Failed to update school",
      };
    } catch {
      return { success: false, error: "An error occurred" };
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
        showToast({ message: "School deactivated", variant: "success" });
      } else {
        // The backend returns an explicit message when a school still has
        // active students or staff — surface it verbatim, inside the dialog.
        setDeactivateError(
          response.message || "This school cannot be deactivated right now",
        );
      }
    } catch {
      setDeactivateError("An error occurred");
    } finally {
      setIsDeactivating(false);
    }
  };

  const columns: TableColumn<DistrictSchool>[] = [
    {
      key: "name",
      header: "School",
      cell: (s) => (
        <span className="font-medium text-brand-slate-800">{s.name}</span>
      ),
      sortValue: (s) => s.name,
    },
    {
      key: "state",
      header: "State",
      hideBelow: "md",
      cell: (s) => s.stateCode || "—",
      sortValue: (s) => s.stateCode || "",
    },
    {
      key: "students",
      header: "Students",
      align: "right",
      cell: (s) => s.activeStudentCount,
      sortValue: (s) => s.activeStudentCount,
    },
    {
      key: "staff",
      header: "Staff",
      align: "right",
      hideBelow: "md",
      cell: (s) => s.activeStaffCount,
      sortValue: (s) => s.activeStaffCount,
    },
  ];

  return (
    <PageLayout
      title="Schools"
      data-testid="district-schools-page"
      actions={
        <Button
          onClick={() => setIsAddOpen(true)}
          data-testid="district-schools-add"
        >
          <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          Add school
        </Button>
      }
    >
      <Table
        label="Schools"
        data-testid="district-schools-table"
        columns={columns}
        rows={schools}
        rowKey={(s) => s.id}
        loading={isLoading}
        defaultSort={{ key: "name", direction: "asc" }}
        rowActionLabel={(s) => s.name}
        rowActions={(s) => [
          {
            label: "Edit",
            icon: <Pencil className="h-3.5 w-3.5" strokeWidth={1.8} />,
            onSelect: () => setEditing(s),
            "data-testid": `district-school-edit-${s.id}`,
          },
          {
            label: "Deactivate",
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
            title="No schools yet"
            description="Add your first school using the Add school button to start building out your district."
          />
        }
      />

      <Modal
        open={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        title="Add a school"
        data-testid="district-schools-add-modal"
      >
        <SchoolForm
          mode="create"
          submitLabel="Add school"
          onSubmit={handleCreate}
          onCancel={() => setIsAddOpen(false)}
          testIdPrefix="district-schools-create"
        />
      </Modal>

      <Modal
        open={editing !== null}
        onClose={() => setEditing(null)}
        title="Edit school"
        data-testid="district-schools-edit-modal"
      >
        {editing && (
          <SchoolForm
            mode="edit"
            initialName={editing.name}
            initialStateCode={editing.stateCode ?? ""}
            submitLabel="Save changes"
            onSubmit={handleUpdate}
            onCancel={() => setEditing(null)}
            testIdPrefix={`district-school-edit-form-${editing.id}`}
          />
        )}
      </Modal>

      <ConfirmDialog
        open={deactivating !== null}
        title="Deactivate school"
        message={
          deactivating
            ? `Deactivate ${deactivating.name}? Staff and students will lose access to it.`
            : ""
        }
        confirmLabel="Deactivate school"
        loading={isDeactivating}
        error={deactivateError}
        onConfirm={confirmDeactivate}
        onCancel={() => setDeactivating(null)}
        data-testid="district-school-deactivate-dialog"
      />
    </PageLayout>
  );
}
