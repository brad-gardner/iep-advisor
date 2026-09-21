import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { orgRoleLabel } from '@/lib/org-role-label';
import {
  ACCESS_ROLES,
  TEAM_ROLES,
  TEAM_ROLE_LABELS,
  defaultAccessRoleForTeamRole,
} from '../../types';
import type { AccessRole, AddTeamMemberRequest, EligibleStaff, TeamRole } from '../../types';
import { eligibleTeamStaff } from './team-eligibility';

const MAX_MATCHES = 50;

// The eligible-staff directory: `null` while loading (or after a failure, with
// `failed` set) so "nobody left to add" is never confused with "no directory".
export interface StaffDirectory {
  staff: EligibleStaff[] | null;
  failed: boolean;
}

interface AddTeamMemberFormProps {
  directory: StaffDirectory;
  studentSchoolId: number;
  // staffProfileIds already on the team (hidden from the picker).
  memberProfileIds: ReadonlySet<number>;
  onAdd: (data: AddTeamMemberRequest) => Promise<{ success: boolean; error?: string }>;
}

function matches(member: EligibleStaff, term: string): boolean {
  const haystack = `${member.firstName} ${member.lastName} ${member.email}`.toLowerCase();
  return haystack.includes(term);
}

export function AddTeamMemberForm({
  directory,
  studentSchoolId,
  memberProfileIds,
  onAdd,
}: AddTeamMemberFormProps) {
  const [search, setSearch] = useState('');
  const [staffProfileId, setStaffProfileId] = useState('');
  const [teamRole, setTeamRole] = useState<TeamRole>('InterventionSpecialist');
  // '' = take the server default for the role.
  const [accessRole, setAccessRole] = useState<'' | AccessRole>('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const eligible = useMemo(
    () => eligibleTeamStaff(directory.staff ?? [], memberProfileIds),
    [directory.staff, memberProfileIds]
  );
  const term = search.trim().toLowerCase();
  const candidates = useMemo(
    () => (term ? eligible.filter((m) => matches(m, term)) : eligible).slice(0, MAX_MATCHES),
    [eligible, term]
  );
  // A search that filters out the chosen person must not leave a hidden
  // selection behind; the picker then reads "Select (n)" and submit refuses.
  const selectedId = candidates.some((c) => String(c.staffProfileId) === staffProfileId)
    ? staffProfileId
    : '';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!selectedId) {
      setError('Select a staff member to add');
      return;
    }
    setIsSubmitting(true);
    const result = await onAdd({
      staffProfileId: Number(selectedId),
      teamRole,
      accessRole: accessRole || undefined,
    });
    if (result.success) {
      setSearch('');
      setStaffProfileId('');
      setAccessRole('');
    } else {
      setError(result.error ?? 'Could not add this team member');
    }
    setIsSubmitting(false);
  };

  if (directory.failed) {
    return (
      <Notice variant="error" title="Staff directory unavailable" data-testid="team-add-unavailable">
        The list of staff who can join this team could not be loaded. Reload the page to try again.
      </Notice>
    );
  }

  if (directory.staff === null) {
    return (
      <div className="space-y-2" data-testid="team-add-loading" aria-busy="true">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-10 w-full" />
      </div>
    );
  }

  if (eligible.length === 0) {
    return (
      <p className="text-sm text-brand-slate-500" data-testid="team-add-empty">
        Everyone eligible at this school is already on the team.
      </p>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="team-add-form">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          id="team-add-search"
          type="search"
          label="Find staff"
          placeholder="Name or email"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          data-testid="team-add-search"
        />
        <Select
          id="team-add-staff"
          label="Staff member *"
          value={selectedId}
          onChange={(e) => setStaffProfileId(e.target.value)}
          data-testid="team-add-staff"
        >
          <option value="">
            {candidates.length === 0 ? 'No matches' : `Select (${candidates.length})`}
          </option>
          {candidates.map((m) => (
            <option key={m.staffProfileId} value={m.staffProfileId}>
              {`${m.firstName} ${m.lastName}`.trim() || m.email} · {orgRoleLabel(m.orgRoleName)}
              {m.schoolId !== studentSchoolId && m.schoolName ? ` · ${m.schoolName}` : ''}
            </option>
          ))}
        </Select>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Select
          id="team-add-role"
          label="Team role *"
          value={teamRole}
          onChange={(e) => setTeamRole(e.target.value as TeamRole)}
          data-testid="team-add-role"
        >
          {TEAM_ROLES.map((role) => (
            <option key={role} value={role}>
              {TEAM_ROLE_LABELS[role]}
            </option>
          ))}
        </Select>
        <Select
          id="team-add-permission"
          label="Permission"
          value={accessRole}
          onChange={(e) => setAccessRole(e.target.value as '' | AccessRole)}
          data-testid="team-add-permission"
        >
          <option value="">Default for role ({defaultAccessRoleForTeamRole(teamRole)})</option>
          {ACCESS_ROLES.map((role) => (
            <option key={role} value={role}>
              {role}
            </option>
          ))}
        </Select>
      </div>

      <Button type="submit" loading={isSubmitting} data-testid="team-add-submit">
        Add member
      </Button>
    </form>
  );
}
