import { InviteRow } from './invite-row';
import type { StaffPendingInvite } from '../types';

interface InvitesListProps {
  invites: StaffPendingInvite[];
  onRevoke: (inviteId: number) => Promise<{ success: boolean; error?: string }>;
  onResend: (
    inviteId: number
  ) => Promise<{ success: boolean; error?: string; inviteUrl?: string | null }>;
}

export function InvitesList({ invites, onRevoke, onResend }: InvitesListProps) {
  if (invites.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm" data-testid="staff-invites-empty">
        No pending invites.
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="staff-invites-list">
      {invites.map((invite) => (
        <InviteRow
          key={invite.id}
          invite={invite}
          onRevoke={onRevoke}
          onResend={onResend}
        />
      ))}
    </ul>
  );
}
