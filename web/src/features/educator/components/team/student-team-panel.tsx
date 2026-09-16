import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Users } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { EmptyState } from '@/components/ui/empty-state';
import { Table } from '@/components/ui/table';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import {
  addTeamMember,
  getEligibleTeamStaff,
  getTeam,
  removeTeamMember,
  setTeamLead,
  updateTeamMember,
} from '../../api/educator-api';
import type { AddTeamMemberRequest, StudentTeamMember, TeamRole } from '../../types';
import { AddTeamMemberForm, type StaffDirectory } from './add-team-member-form';
import { teamMemberActions, teamMemberColumns } from './team-columns';
import { teamMemberName } from './team-eligibility';

interface StudentTeamPanelProps {
  studentId: number;
  studentSchoolId: number;
  // Admins always manage; otherwise the current lead case manager may.
  isAdmin: boolean;
  currentUserId?: number | null;
  // Fired after any successful mutation so the host can refresh the
  // student's `caseManagerName`.
  onTeamChanged?: () => void;
  // Fired whenever the member list is (re)loaded or adopted, so the host can
  // derive the caller's own membership (e.g. their access role).
  onMembersChange?: (members: StudentTeamMember[]) => void;
  // "Family" section content (parent links), rendered below the team.
  family?: React.ReactNode;
}

async function safe<T>(
  call: () => Promise<{ success: boolean; message?: string; data?: T }>,
  fallback: string
) {
  try {
    const response = await call();
    return response.success
      ? { success: true as const, data: response.data }
      : { success: false as const, error: response.message || fallback };
  } catch (err) {
    return { success: false as const, error: apiErrorMessage(err, fallback) };
  }
}

// Server order is lead first; keep it when a DTO is adopted locally.
function leadFirst(members: StudentTeamMember[]): StudentTeamMember[] {
  return [...members].sort((a, b) => Number(b.isLead) - Number(a.isLead));
}

// Merge a returned DTO into the list (replace or append). A new lead demotes
// the previous one, mirroring the server's single-active-lead invariant.
function adopt(members: StudentTeamMember[] | null, dto: StudentTeamMember): StudentTeamMember[] {
  const list = members ?? [];
  const merged = list.some((m) => m.id === dto.id)
    ? list.map((m) => (m.id === dto.id ? dto : m))
    : [...list, dto];
  return leadFirst(
    dto.isLead ? merged.map((m) => (m.id !== dto.id && m.isLead ? { ...m, isLead: false } : m)) : merged
  );
}

// The IEP team: who works with this student, in what functional role, with
// what effective permission. Lead case manager first. Managers can add,
// re-role, promote and remove members inline.
export function StudentTeamPanel({
  studentId,
  studentSchoolId,
  isAdmin,
  currentUserId,
  onTeamChanged,
  onMembersChange,
  family,
}: StudentTeamPanelProps) {
  const { show: showToast } = useToast();
  const [members, setMembers] = useState<StudentTeamMember[] | null>(null);
  const [directory, setDirectory] = useState<StaffDirectory>({ staff: null, failed: false });
  // Bumped after add/remove so the eligible-staff directory is refetched.
  const [directoryVersion, setDirectoryVersion] = useState(0);
  // Member id → the role the user just chose, shown while the PUT is in
  // flight (item-scoped so two rows can save independently).
  const [pendingRoles, setPendingRoles] = useState<ReadonlyMap<number, TeamRole>>(new Map());
  const [removing, setRemoving] = useState<StudentTeamMember | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  // Generation counter for GET /team: only the newest request may write.
  const teamSeq = useRef(0);

  const lead = members?.find((m) => m.isLead) ?? null;
  const canManage = isAdmin || (currentUserId != null && lead?.userId === currentUserId);

  const reload = useCallback(async () => {
    const mine = ++teamSeq.current;
    const result = await safe(() => getTeam(studentId), 'Could not load the team');
    if (mine !== teamSeq.current) return;
    setMembers(result.success && result.data ? result.data : []);
  }, [studentId]);

  // A transfer deactivates off-school members server-side, so the list is
  // refetched when the school changes too; any older in-flight GET is dropped.
  useEffect(() => {
    void reload();
    return () => {
      teamSeq.current += 1;
    };
  }, [reload, studentSchoolId]);

  useEffect(() => {
    if (members) onMembersChange?.(members);
  }, [members, onMembersChange]);

  // Only managers need the directory (for the add-member picker).
  useEffect(() => {
    if (!canManage) return;
    let active = true;
    (async () => {
      const result = await safe(
        () => getEligibleTeamStaff(studentId),
        'Staff directory unavailable'
      );
      if (!active) return;
      setDirectory(
        result.success && result.data
          ? { staff: result.data, failed: false }
          : { staff: null, failed: true }
      );
    })();
    return () => {
      active = false;
    };
  }, [canManage, studentId, studentSchoolId, directoryVersion]);

  const afterChange = (message: string) => {
    onTeamChanged?.();
    showToast({ message, variant: 'success' });
  };

  const handleAdd = async (data: AddTeamMemberRequest) => {
    const result = await safe(
      () => addTeamMember(studentId, data),
      'Could not add this team member'
    );
    if (result.success && result.data) {
      const dto = result.data;
      setMembers((prev) => adopt(prev, dto));
      setDirectoryVersion((v) => v + 1);
      afterChange('Team member added');
    }
    return { success: result.success, error: result.error };
  };

  const handleRoleChange = async (member: StudentTeamMember, teamRole: TeamRole) => {
    setPendingRoles((prev) => new Map(prev).set(member.id, teamRole));
    const result = await safe(
      () => updateTeamMember(studentId, member.id, { teamRole }),
      'Could not update the role'
    );
    if (result.success && result.data) {
      const dto = result.data;
      setMembers((prev) => adopt(prev, dto));
      afterChange('Team role updated');
    } else {
      showToast({ message: result.error ?? 'Could not update the role', variant: 'error' });
    }
    setPendingRoles((prev) => {
      const next = new Map(prev);
      next.delete(member.id);
      return next;
    });
  };

  const handleMakeLead = async (member: StudentTeamMember) => {
    const result = await safe(
      () => setTeamLead(studentId, member.id),
      'Could not change the lead'
    );
    if (result.success && result.data) {
      const dto = result.data;
      setMembers((prev) => adopt(prev, dto));
      afterChange(`${teamMemberName(member)} is now the lead case manager`);
    } else {
      showToast({ message: result.error ?? 'Could not change the lead', variant: 'error' });
    }
  };

  const confirmRemove = async () => {
    if (!removing) return;
    const target = removing;
    setIsRemoving(true);
    setRemoveError(null);
    const result = await safe(
      () => removeTeamMember(studentId, target.id),
      'Could not remove this member'
    );
    if (result.success) {
      setRemoving(null);
      setMembers((prev) => (prev ?? []).filter((m) => m.id !== target.id));
      setDirectoryVersion((v) => v + 1);
      afterChange('Team member removed');
    } else {
      // e.g. "Choose a new lead case manager first." — stays in the dialog.
      setRemoveError(result.error ?? 'Could not remove this member');
    }
    setIsRemoving(false);
  };

  const memberProfileIds = useMemo(
    () => new Set((members ?? []).map((m) => m.staffProfileId)),
    [members]
  );
  // Cheap to rebuild per render; the Table's own memo keys off identity only
  // for sorting, which this list does not use.
  const columns = teamMemberColumns({ canManage, pendingRoles, onRoleChange: handleRoleChange });

  return (
    <Card data-testid="student-team-panel" className="space-y-6">
      <div className="space-y-3">
        {lead === null && members !== null && members.length > 0 && (
          <p className="text-sm text-brand-amber-600" data-testid="team-no-lead">
            No lead case manager yet — choose "Make lead" on a member.
          </p>
        )}
        <Table
          label="IEP team"
          data-testid="student-team-list"
          columns={columns}
          rows={members ?? []}
          rowKey={(m) => m.id}
          loading={members === null}
          loadingRows={2}
          rowActionLabel={teamMemberName}
          rowActions={
            canManage
              ? (m) =>
                  teamMemberActions(m, {
                    onMakeLead: handleMakeLead,
                    onRemove: (member) => {
                      setRemoveError(null);
                      setRemoving(member);
                    },
                  })
              : undefined
          }
          empty={
            <EmptyState
              data-testid="student-team-empty"
              icon={Users}
              title="No team yet"
              description={
                canManage
                  ? 'Add the case manager and the staff who work with this student.'
                  : 'An administrator can build this student’s IEP team.'
              }
            />
          }
        />

        {canManage && (
          <div className="border-t border-brand-slate-100 pt-4">
            <h3 className="mb-3 text-sm font-medium text-brand-slate-800">Add member</h3>
            <AddTeamMemberForm
              directory={directory}
              studentSchoolId={studentSchoolId}
              memberProfileIds={memberProfileIds}
              onAdd={handleAdd}
            />
          </div>
        )}
      </div>

      {family && (
        <section className="border-t border-brand-slate-100 pt-4" data-testid="student-family-section">
          {family}
        </section>
      )}

      <ConfirmDialog
        open={removing !== null}
        title="Remove team member"
        message={
          removing
            ? `Remove ${teamMemberName(removing)} from this student's team? Their access to the student is revoked.`
            : ''
        }
        confirmLabel="Remove member"
        loading={isRemoving}
        error={removeError}
        onConfirm={confirmRemove}
        onCancel={() => setRemoving(null)}
        data-testid="team-remove-dialog"
      />
    </Card>
  );
}
