import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';
import { InviteUrlField } from './invite-url-field';
import { orgRoleLabel } from '@/lib/org-role-label';
import type { StaffPendingInvite } from '../types';

interface InviteRowProps {
  invite: StaffPendingInvite;
  onRevoke: (inviteId: number) => Promise<{ success: boolean; error?: string }>;
  // Resend may return a fresh inviteUrl (dev-only); surface it inline when present.
  onResend: (
    inviteId: number
  ) => Promise<{ success: boolean; error?: string; inviteUrl?: string | null }>;
}

function formatExpiry(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function InviteRow({ invite, onRevoke, onResend }: InviteRowProps) {
  const [isConfirmingRevoke, setIsConfirmingRevoke] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);

  const isExpired = invite.status === 'expired';

  const handleRevoke = async () => {
    setIsSubmitting(true);
    setError(null);
    const result = await onRevoke(invite.id);
    if (!result.success) {
      setError(result.error ?? 'Could not revoke this invite');
    }
    setIsConfirmingRevoke(false);
    setIsSubmitting(false);
  };

  const handleResend = async () => {
    setIsSubmitting(true);
    setError(null);
    const result = await onResend(invite.id);
    if (result.success) {
      setInviteUrl(result.inviteUrl ?? null);
    } else {
      setError(result.error ?? 'Could not resend this invite');
    }
    setIsSubmitting(false);
  };

  return (
    <li>
      <Card data-testid={`staff-invite-${invite.id}`}>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className="text-brand-slate-800 font-medium">{invite.email}</span>
              {isExpired && <Badge variant="error">Expired</Badge>}
            </div>
            <p className="text-xs text-brand-slate-400">
              {orgRoleLabel(invite.orgRoleName)}
              {invite.schoolName ? ` · ${invite.schoolName}` : ' · District-wide'}
              {' · '}
              {isExpired ? 'Expired' : 'Expires'} {formatExpiry(invite.inviteExpiresAt)}
            </p>
            {error && (
              <div className="pt-2">
                <Notice variant="error" title={error} />
              </div>
            )}
          </div>

          <div className="flex shrink-0 items-center gap-2">
            <Button
              variant="secondary"
              onClick={handleResend}
              disabled={isSubmitting}
              data-testid={`staff-invite-resend-${invite.id}`}
            >
              Resend
            </Button>
            {isConfirmingRevoke ? (
              <>
                <Button
                  variant="danger"
                  onClick={handleRevoke}
                  disabled={isSubmitting}
                  data-testid={`staff-invite-revoke-confirm-${invite.id}`}
                >
                  {isSubmitting ? 'Revoking...' : 'Confirm'}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => setIsConfirmingRevoke(false)}
                  disabled={isSubmitting}
                  data-testid={`staff-invite-revoke-cancel-${invite.id}`}
                >
                  Cancel
                </Button>
              </>
            ) : (
              <Button
                variant="danger"
                onClick={() => {
                  setError(null);
                  setIsConfirmingRevoke(true);
                }}
                data-testid={`staff-invite-revoke-${invite.id}`}
              >
                Revoke
              </Button>
            )}
          </div>
        </div>

        {inviteUrl && <InviteUrlField url={inviteUrl} />}
      </Card>
    </li>
  );
}
