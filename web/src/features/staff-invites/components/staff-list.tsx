import { StaffRow } from './staff-row';
import type { StaffMember } from '../types';

interface StaffListProps {
  members: StaffMember[];
  onDeactivate: (staffProfileId: number) => Promise<{ success: boolean; error?: string }>;
  onReactivate: (staffProfileId: number) => Promise<{ success: boolean; error?: string }>;
}

export function StaffList({ members, onDeactivate, onReactivate }: StaffListProps) {
  if (members.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm" data-testid="district-staff-empty">
        No staff yet. Invite someone to get started.
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="district-staff-list">
      {members.map((member) => (
        <StaffRow
          key={member.staffProfileId}
          member={member}
          onDeactivate={onDeactivate}
          onReactivate={onReactivate}
        />
      ))}
    </ul>
  );
}
