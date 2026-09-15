import { useCallback, useEffect, useMemo, useState } from 'react';
import { Users } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { EmptyState } from '@/components/ui/empty-state';
import { Table } from '@/components/ui/table';
import { useToast } from '@/components/ui/toast';
import { getStaffList } from '@/features/staff-invites/api/staff-invites-api';
import type { StaffMember } from '@/features/staff-invites/types';
import {
  addTeamMember,
  getTeam,
  removeTeamMember,
  setTeamLead,
  updateTeamMember,
} from '../../api/educator-api';
import type { AddTeamMemberRequest, StudentTeamMember, TeamRole } from '../../types';
import { AddTeamMemberForm } from './add-team-member-form';
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
  // "Family" section content (parent links), rendered below the team.
  family?: React.ReactNode;
}

async function safe<T>(call: () => Promise<{ success: boolean; message?: string; data?: T }>) {
  try {
    const response = await call();
    return response.success
      ? { success: true as const, data: response.data }
      : { success: false as const, error: response.message };
  } catch {
    return { success: false as const, error: 'An error occurred' };
  }
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
  family,
}: StudentTeamPanelProps) {
  const { show: showToast } = useToast();
  const [members, setMembers] = useState<StudentTeamMember[] | null>(null);
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [savingIds, setSavingIds] = useState<ReadonlySet<number>>(new Set());
  const [removing, setRemoving] = useState<StudentTeamMember | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);

  const lead = members?.find((m) => m.isLead) ?? null;
  const canManage = isAdmin || (currentUserId != null && lead?.userId === currentUserId);

  const reload = useCallback(async () => {
    const result = await safe(() => getTeam(studentId));
    setMembers(result.success && result.data ? result.data : []);
  }, [studentId]);

  useEffect(() => {
    let active = true;
    (async () => {
      const result = await safe(() => getTeam(studentId));
      if (active) setMembers(result.success && result.data ? result.data : []);
    })();
    return () => {
      active = false;
    };
  }, [studentId]);

  // Only managers need the directory (for the add-member picker).
  useEffect(() => {
    if (!canManage) return;
    let active = true;
    (async () => {
      const result = await safe(() => getStaffList());
      if (active) setStaff(result.success && result.data ? result.data.members : []);
    })();
    return () => {
      active = false;
    };
  }, [canManage]);

  const afterChange = async (message: string) => {
    await reload();
    onTeamChanged?.();
    showToast({ message, variant: 'success' });
  };

  const handleAdd = async (data: AddTeamMemberRequest) => {
    const result = await safe(() => addTeamMember(studentId, data));
    if (result.success) await afterChange('Team member added');
    return { success: result.success, error: result.error };
  };

  const handleRoleChange = async (member: StudentTeamMember, teamRole: TeamRole) => {
    setSavingIds((prev) => new Set(prev).add(member.id));
    const result = await safe(() => updateTeamMember(studentId, member.id, { teamRole }));
    if (result.success) await afterChange('Team role updated');
    else showToast({ message: result.error ?? 'Could not update the role', variant: 'error' });
    setSavingIds((prev) => {
      const next = new Set(prev);
      next.delete(member.id);
      return next;
    });
  };

  const handleMakeLead = async (member: StudentTeamMember) => {
    const result = await safe(() => setTeamLead(studentId, member.id));
    if (result.success) await afterChange(`${teamMemberName(member)} is now the lead case manager`);
    else showToast({ message: result.error ?? 'Could not change the lead', variant: 'error' });
  };

  const confirmRemove = async () => {
    if (!removing) return;
    setIsRemoving(true);
    setRemoveError(null);
    const result = await safe(() => removeTeamMember(studentId, removing.id));
    if (result.success) {
      setRemoving(null);
      await afterChange('Team member removed');
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
  const columns = teamMemberColumns({ canManage, savingIds, onRoleChange: handleRoleChange });

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
              staff={staff}
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
