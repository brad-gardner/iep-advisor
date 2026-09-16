import { ShieldCheck, Star } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/input';
import { orgRoleLabel } from '@/lib/org-role-label';
import { TEAM_ROLES, TEAM_ROLE_LABELS } from '../../types';
import type { StudentTeamMember, TeamRole } from '../../types';
import { teamMemberName } from './team-eligibility';

// Effective permission chip. Deliberately styled unlike the team role (icon +
// slate "info" badge vs. plain role text / select) so the two axes never blur.
export function PermissionBadge({ member }: { member: StudentTeamMember }) {
  return (
    <Badge variant="info" data-testid={`team-permission-${member.id}`}>
      <ShieldCheck className="mr-1 h-3 w-3" strokeWidth={2} aria-hidden="true" />
      {member.accessRole}
    </Badge>
  );
}

export function LeadBadge() {
  return (
    <Badge variant="success" data-testid="team-lead-badge">
      <Star className="mr-1 h-3 w-3" strokeWidth={2} aria-hidden="true" />
      Lead case manager
    </Badge>
  );
}

// Name + email + org role, with the lead marker inline so the lead row reads
// as such even with the Permission column hidden on small screens.
export function TeamMemberCell({ member }: { member: StudentTeamMember }) {
  return (
    <div className="space-y-0.5">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium text-brand-slate-800">{teamMemberName(member)}</span>
        {member.isLead && <LeadBadge />}
      </div>
      <p className="text-xs text-brand-slate-500">{member.email}</p>
      <p className="text-xs text-brand-slate-400">{orgRoleLabel(member.orgRoleName)}</p>
    </div>
  );
}

interface TeamRoleCellProps {
  member: StudentTeamMember;
  canManage: boolean;
  // The role chosen but not yet saved; shown in place of `member.teamRole`
  // while the PUT is in flight.
  pendingRole?: TeamRole;
  onChange: (teamRole: TeamRole) => void;
}

// Inline role edit for managers; plain label for everyone else. The select
// stays enabled (aria-busy) while saving so keyboard focus is not dropped;
// changes made mid-save are ignored.
export function TeamRoleCell({ member, canManage, pendingRole, onChange }: TeamRoleCellProps) {
  if (!canManage) {
    return <span data-testid={`team-role-${member.id}`}>{TEAM_ROLE_LABELS[member.teamRole]}</span>;
  }
  const saving = pendingRole !== undefined;
  return (
    <Select
      id={`team-role-${member.id}`}
      aria-label={`Team role for ${teamMemberName(member)}`}
      value={pendingRole ?? member.teamRole}
      aria-busy={saving || undefined}
      onChange={(e) => {
        if (!saving) onChange(e.target.value as TeamRole);
      }}
      className="min-w-44"
      data-testid={`team-role-${member.id}`}
    >
      {TEAM_ROLES.map((role) => (
        <option key={role} value={role}>
          {TEAM_ROLE_LABELS[role]}
        </option>
      ))}
    </Select>
  );
}
