import { Star, Trash2 } from 'lucide-react';
import type { MenuItem } from '@/components/ui/menu';
import type { TableColumn } from '@/components/ui/table';
import type { StudentTeamMember, TeamRole } from '../../types';
import { PermissionBadge, TeamMemberCell, TeamRoleCell } from './team-member-row';

interface ColumnOptions {
  canManage: boolean;
  // Member ids with an in-flight role change (item-scoped, so two rows can
  // update independently).
  savingIds: ReadonlySet<number>;
  onRoleChange: (member: StudentTeamMember, teamRole: TeamRole) => void;
}

// Column set for the team Table. Lead-first ordering comes from the row
// order (the API returns lead first; no `sortValue`, so it holds).
export function teamMemberColumns({
  canManage,
  savingIds,
  onRoleChange,
}: ColumnOptions): TableColumn<StudentTeamMember>[] {
  return [
    { key: 'member', header: 'Member', cell: (m) => <TeamMemberCell member={m} /> },
    {
      key: 'teamRole',
      header: 'Team role',
      cell: (m) => (
        <TeamRoleCell
          member={m}
          canManage={canManage}
          saving={savingIds.has(m.id)}
          onChange={(teamRole) => onRoleChange(m, teamRole)}
        />
      ),
    },
    {
      key: 'permission',
      header: 'Permission',
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
  const items: MenuItem[] = [];
  if (!member.isLead) {
    items.push({
      label: 'Make lead',
      icon: <Star className="h-3.5 w-3.5" strokeWidth={1.8} />,
      onSelect: () => onMakeLead(member),
      'data-testid': `team-make-lead-${member.id}`,
    });
  }
  items.push({
    label: 'Remove',
    icon: <Trash2 className="h-3.5 w-3.5" strokeWidth={1.8} />,
    variant: 'danger',
    onSelect: () => onRemove(member),
    'data-testid': `team-remove-${member.id}`,
  });
  return items;
}
