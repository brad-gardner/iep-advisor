import { Star, Trash2 } from 'lucide-react';
import type { MenuItem } from '@/components/ui/menu';
import type { TableColumn } from '@/components/ui/table';
import i18n from '@/lib/i18n';
import type { StudentTeamMember, TeamRole } from '../../types';
import { PermissionBadge, TeamMemberCell, TeamRoleCell } from './team-member-row';

interface ColumnOptions {
  canManage: boolean;
  // Member id → role chosen but not yet saved (item-scoped, so two rows can
  // update independently and the select never snaps back mid-save).
  pendingRoles: ReadonlyMap<number, TeamRole>;
  onRoleChange: (member: StudentTeamMember, teamRole: TeamRole) => void;
}

// Column set for the team Table. Lead-first ordering comes from the row
// order (the API returns lead first; no `sortValue`, so it holds). Header
// text uses the plain `i18n.t` singleton (not a hook — this is a builder
// function, not a component); the host (`StudentTeamPanel`) calls this
// fresh on every render rather than memoizing, so a language switch is
// reflected immediately.
export function teamMemberColumns({
  canManage,
  pendingRoles,
  onRoleChange,
}: ColumnOptions): TableColumn<StudentTeamMember>[] {
  const t = i18n.t;
  return [
    { key: 'member', header: t('educator:team.memberColumn'), cell: (m) => <TeamMemberCell member={m} /> },
    {
      key: 'teamRole',
      header: t('educator:team.teamRoleColumn'),
      cell: (m) => (
        <TeamRoleCell
          member={m}
          canManage={canManage}
          pendingRole={pendingRoles.get(m.id)}
          onChange={(teamRole) => onRoleChange(m, teamRole)}
        />
      ),
    },
    {
      key: 'permission',
      header: t('educator:team.permissionColumn'),
      hideBelow: 'md',
      cell: (m) => <PermissionBadge member={m} />,
    },
  ];
}

interface ActionOptions {
  onMakeLead: (member: StudentTeamMember) => void;
  onRemove: (member: StudentTeamMember) => void;
}

export function teamMemberActions(
  member: StudentTeamMember,
  { onMakeLead, onRemove }: ActionOptions
): MenuItem[] {
  const t = i18n.t;
  const items: MenuItem[] = [];
  if (!member.isLead) {
    items.push({
      label: t('educator:team.makeLead'),
      icon: <Star className="h-3.5 w-3.5" strokeWidth={1.8} />,
      onSelect: () => onMakeLead(member),
      'data-testid': `team-make-lead-${member.id}`,
    });
  }
  items.push({
    label: t('educator:team.remove'),
    icon: <Trash2 className="h-3.5 w-3.5" strokeWidth={1.8} />,
    variant: 'danger',
    onSelect: () => onRemove(member),
    'data-testid': `team-remove-${member.id}`,
  });
  return items;
}
