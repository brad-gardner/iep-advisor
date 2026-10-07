import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
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
import { ORG_ROLE, isAdminOrgRole, isCaseloadOrgRole } from "../types";
import { attentionFilterLabel } from "../lib/student-enum-labels";
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

export function EducatorStudentsPage() {
  // `educator` is a staff-only namespace (plan phase 5): its English is NOT
  // in the main chunk — it's registered by `@/app/lazy-routes/staff-locales`,
  // imported at the top of this page's lazy route chunk
  // (`app/lazy-routes/staff-routes.tsx`) — and its Spanish still lazy-loads
  // like any other namespace. Converted fully in plan phase 5 (this page was
  // the staff-namespace worked example in an earlier phase; see
  // `docs/i18n/README.md`'s "Staff and admin namespaces").
  const { t } = useTranslation(['educator', 'common']);
  usePageTitle(t('studentsPage.title'));
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
      ? t('studentsPage.dueBetween', { from: query.from, to: query.to })
      : query.attention
        ? attentionFilterLabel(query.attention)
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
        showToast({ message: t('studentsPage.studentAdded'), variant: "success" });
        return { success: true };
      }
      return { success: false, error: response.message || t('studentsPage.addStudentFailed') };
    } catch (err) {
      return { success: false, error: apiErrorMessage(err, t('studentsPage.addStudentFailed')) };
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
          message: t('studentsPage.caseManagerAssigned', { count: updated }),
          variant: "success",
        });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || t('studentsPage.assignCaseManagerFailed'),
      };
    } catch (err) {
      return {
        success: false,
        error: apiErrorMessage(err, t('studentsPage.assignCaseManagerFailed')),
      };
    }
  };

  // Not memoized: `rosterColumns` reads the current language through the
  // plain `i18n.t` singleton (it's a builder function, not a component), so
  // memoizing on `[isDistrictAdmin]` alone would keep stale-language headers
  // across a language switch — see `admin-notification-failures-page.tsx` for
  // the same "recompute every render" convention with translated columns.
  const columns = rosterColumns({ showSchool: isDistrictAdmin });

  return (
    <PageLayout
      title={t('studentsPage.title')}
      data-testid="educator-students-page"
      actions={
        <div className="flex items-center gap-2">
          {isAdmin && (
            <Link to="/educator/admin/imports" data-testid="educator-students-import">
              <Button variant="secondary">
                <Upload className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
                {t('studentsPage.import')}
              </Button>
            </Link>
          )}
          <Button
            onClick={() => setIsAddOpen(true)}
            data-testid="educator-students-add"
          >
            <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
            {t('studentsPage.addStudent')}
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
            {t('studentsPage.showingAttention', { label: attentionLabel })}
          </span>
          <Button
            variant="ghost"
            size="sm"
            onClick={clearAttention}
            data-testid="attention-filter-clear"
          >
            {t('studentsPage.clear')}
          </Button>
        </div>
      )}

      {failed && (
        <Notice variant="error" title={t('studentsPage.couldNotLoad')}>
          <Button
            variant="secondary"
            size="sm"
            className="mt-2"
            onClick={refresh}
            data-testid="educator-students-retry"
          >
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
      )}

      {isAdmin && (
        <>
          {/* Always mounted so AT announces the very first selection too. */}
          <p className="sr-only" aria-live="polite" data-testid="roster-selection-status">
            {selection.selectedIds.size > 0
              ? t('studentsPage.selectedCount', { count: selection.selectedIds.size })
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
        label={t('studentsPage.title')}
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
            title={t('studentsPage.noStudentsFoundTitle')}
            description={
              isCaseload
                ? t('studentsPage.caseloadEmptyState')
                : t('studentsPage.noStudentsFoundDescription')
            }
          />
        }
      />

      <Pagination
        label={t('studentsPage.paginationLabel')}
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
        title={t('studentsPage.addStudentModalTitle')}
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
