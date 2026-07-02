import { MailPlus } from 'lucide-react';
import { EmptyState } from '@/components/ui/empty-state';
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
      <EmptyState
        data-testid="staff-invites-empty"
        icon={MailPlus}
        title="No pending invites"
        description="Invites you send will appear here until they're accepted."
      />
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
