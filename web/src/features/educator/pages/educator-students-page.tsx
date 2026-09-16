import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { GraduationCap, Plus, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { Notice } from "@/components/ui/notice";
import { PageLayout } from "@/components/ui/page-layout";
import { Pagination } from "@/components/ui/pagination";
import { usePageTitle } from "@/hooks/use-page-title";
import { Table } from "@/components/ui/table";
import { useToast } from "@/components/ui/toast";
import { apiErrorMessage } from "@/lib/api-error";
import { assignCaseManagerBulk, createStudent } from "../api/educator-api";
import { getDistrictSchools } from "@/features/district-admin/api/district-api";
import type { DistrictSchool } from "@/features/district-admin/types";
import type { CreateSchoolStudentRequest, StudentSearchParams } from "../types";
import {
  ATTENTION_FILTER_LABELS,
  ORG_ROLE,
  isAdminOrgRole,
  isCaseloadOrgRole,
} from "../types";
import { useEducatorProfile } from "../hooks/use-educator-profile";
import { useRosterQuery } from "../hooks/use-roster-query";
import { useStudentSearch } from "../hooks/use-student-search";
import { useRosterSelection } from "../hooks/use-roster-selection";
import { CreateStudentForm } from "../components/create-student-form";
import { RosterFilters } from "../components/roster/roster-filters";
import { RosterBulkBar } from "../components/roster/roster-bulk-bar";
import { AssignCaseManagerModal } from "../components/roster/assign-case-manager-modal";
import {
  rosterColumns,
  studentDisplayName,
} from "../components/roster/roster-columns";

const CASELOAD_EMPTY =
  "No students on your caseload yet — your school admin can add you to a student's IEP team, or create one.";

export function EducatorStudentsPage() {
  usePageTitle("Students");
  const { show: showToast } = useToast();
  const { profile } = useEducatorProfile();
  const isDistrictAdmin = profile?.orgRoleId === ORG_ROLE.DistrictAdmin;
  const isAdmin = isAdminOrgRole(profile?.orgRoleId);
  const isCaseload = isCaseloadOrgRole(profile?.orgRoleId);

  const { query, update, clearAttention } = useRosterQuery();
  // The dashboard "needs attention" deep link: the server narrows the roster
  // (same predicate as the tiles), so filters and paging compose as usual.
  // `DueInRange` carries its own caller-chosen window, so its label is built
  // from the query's `from`/`to` rather than the static lookup table.
  const attentionLabel =
    query.attention === 'DueInRange' && query.from && query.to
      ? `due between ${query.from} and ${query.to}`
      : query.attention
        ? ATTENTION_FILTER_LABELS[query.attention]
        : undefined;

  const request = useMemo<StudentSearchParams>(
    () => ({
      query: query.q.trim() || undefined,
      schoolId: isDistrictAdmin && query.schoolId ? query.schoolId : undefined,
      status: query.status,
      grade: query.grade || undefined,
      attention: query.attention ?? undefined,
      from: query.from ?? undefined,
      to: query.to ?? undefined,
      page: query.page,
      pageSize: query.pageSize,
    }),
    [query, isDistrictAdmin],
  );
  const { page, isLoading, failed, refresh } = useStudentSearch(request);
  const selection = useRosterSelection();

  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [isAssignOpen, setIsAssignOpen] = useState(false);

  // DistrictAdmin needs the district's schools for both the create-form picker
  // and the roster filter. Other roles never see a school picker.
  useEffect(() => {
    if (!isDistrictAdmin) return;
    let active = true;
    (async () => {
      try {
        const response = await getDistrictSchools();
        if (active && response.success && response.data) {
          setSchools(response.data);
        }
      } catch {
        if (active) setSchools([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [isDistrictAdmin]);

  const handleCreate = async (data: CreateSchoolStudentRequest) => {
    try {
      const response = await createStudent(data);
      if (response.success) {
        refresh();
        setIsAddOpen(false);
        showToast({ message: "Student added", variant: "success" });
        return { success: true };
      }
      return { success: false, error: response.message || "Failed to add student" };
    } catch (err) {
      return { success: false, error: apiErrorMessage(err, "Failed to add student") };
    }
  };

  const handleAssignCaseManager = async (userId: number) => {
    try {
      const response = await assignCaseManagerBulk({
        studentIds: [...selection.selectedIds],
        userId,
      });
      if (response.success) {
        const updated = response.data?.updated ?? selection.selectedIds.size;
        selection.clear();
        setIsAssignOpen(false);
        refresh();
        showToast({
          message: `Case manager assigned to ${updated} ${updated === 1 ? "student" : "students"}`,
          variant: "success",
        });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || "Could not assign the case manager",
      };
    } catch (err) {
      return {
        success: false,
        error: apiErrorMessage(err, "Could not assign the case manager"),
      };
    }
  };

  const columns = useMemo(
    () => rosterColumns({ showSchool: isDistrictAdmin }),
    [isDistrictAdmin],
  );

  return (
    <PageLayout
      title="Students"
      data-testid="educator-students-page"
      actions={
        <div className="flex items-center gap-2">
          {isAdmin && (
            <Link to="/educator/admin/imports" data-testid="educator-students-import">
              <Button variant="secondary">
                <Upload className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
                Import
              </Button>
            </Link>
          )}
          <Button
            onClick={() => setIsAddOpen(true)}
            data-testid="educator-students-add"
          >
            <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
            Add student
          </Button>
        </div>
      }
    >
      <RosterFilters
        value={query}
        onChange={(patch) => {
          // A new filter set invalidates any cross-page selection.
          if (Object.keys(patch).some((k) => k !== "page")) selection.clear();
          update(patch);
        }}
        schools={isDistrictAdmin ? schools : undefined}
      />

      {attentionLabel && (
        <div
          className="flex items-center justify-between gap-3 rounded-card border border-brand-amber-100 bg-brand-amber-50 px-4 py-2 text-sm"
          data-testid="attention-filter-indicator"
        >
          <span className="text-brand-amber-600">
            Showing students with {attentionLabel}
          </span>
          <Button
            variant="ghost"
            size="sm"
            onClick={clearAttention}
            data-testid="attention-filter-clear"
          >
            Clear
          </Button>
        </div>
      )}

      {failed && (
        <Notice variant="error" title="Couldn't load students">
          <Button
            variant="secondary"
            size="sm"
            className="mt-2"
            onClick={refresh}
            data-testid="educator-students-retry"
          >
            Try again
          </Button>
        </Notice>
      )}

      {isAdmin && (
        <>
          {/* Always mounted so AT announces the very first selection too. */}
          <p className="sr-only" aria-live="polite" data-testid="roster-selection-status">
            {selection.selectedIds.size > 0
              ? `${selection.selectedIds.size} selected`
              : ""}
          </p>
          <RosterBulkBar
            selectedCount={selection.selectedIds.size}
            onAssignCaseManager={() => setIsAssignOpen(true)}
            onClear={selection.clear}
          />
        </>
      )}

      <Table
        label="Students"
        data-testid="student-list"
        columns={columns}
        rows={page.items}
        rowKey={(s) => s.id}
        rowHref={(s) => `/educator/students/${s.id}`}
        loading={isLoading}
        defaultSort={{ key: "name", direction: "asc" }}
        selection={
          isAdmin
            ? {
                selectedKeys: selection.selectedIds,
                onToggle: selection.toggle,
                onToggleAll: selection.toggleAll,
                rowLabel: studentDisplayName,
              }
            : undefined
        }
        empty={
          <EmptyState
            data-testid="student-list-empty"
            icon={GraduationCap}
            title="No students found"
            description={
              isCaseload
                ? CASELOAD_EMPTY
                : "Adjust the filters, add a student, or import a roster."
            }
          />
        }
      />

      <Pagination
        label="Students pagination"
        page={query.page}
        pageSize={query.pageSize}
        total={page.total}
        onPageChange={(next) => update({ page: next })}
        onPageSizeChange={(size) => update({ pageSize: size })}
        data-testid="student-list-pagination"
      />

      <Modal
        open={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        title="Add a student"
        data-testid="educator-students-add-modal"
      >
        <CreateStudentForm
          onSubmit={handleCreate}
          schools={isDistrictAdmin ? schools : undefined}
          embedded
        />
      </Modal>

      <AssignCaseManagerModal
        open={isAssignOpen}
        studentCount={selection.selectedIds.size}
        onClose={() => setIsAssignOpen(false)}
        onAssign={handleAssignCaseManager}
      />
    </PageLayout>
  );
}
