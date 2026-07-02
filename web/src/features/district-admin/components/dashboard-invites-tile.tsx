import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { DashboardInvite } from '../types';

interface DashboardInvitesTileProps {
  // Pending + expired invites, expired-first (server-ordered).
  invites: DashboardInvite[];
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

// Invites needing attention (pending + expired, expired flagged). Presentational:
// the composing container owns the single dashboard fetch.
export function DashboardInvitesTile({ invites }: DashboardInvitesTileProps) {
  return (
    <Card data-testid="dashboard-invites-tile">
      <h2 className="font-serif text-xl mb-4">Invites</h2>

      {invites.length === 0 ? (
        <p
          className="text-sm text-brand-slate-400"
          data-testid="dashboard-invites-tile-empty"
        >
          No invites need attention.
        </p>
      ) : (
        <ul className="space-y-3 text-sm">
          {invites.map((invite) => {
            const isExpired = invite.status === 'expired';
            return (
              <li key={invite.id} data-testid={`dashboard-invite-${invite.id}`}>
                <div className="flex items-center gap-2">
                  <span className="text-brand-slate-800 font-medium">
                    {invite.email}
                  </span>
                  {isExpired && <Badge variant="error">Expired</Badge>}
                </div>
                <p className="text-xs text-brand-slate-400">
                  {invite.orgRoleName}
                  {invite.schoolName ? ` · ${invite.schoolName}` : ' · District-wide'}
                  {' · '}
                  {isExpired ? 'Expired on' : 'Expires'} {formatExpiry(invite.inviteExpiresAt)}
                </p>
              </li>
            );
          })}
        </ul>
      )}

      <div className="mt-4">
        <Link
          to="/educator/admin/staff"
          className="text-sm text-brand-teal-600 hover:underline"
          data-testid="dashboard-invites-tile-link"
        >
          Manage invites
        </Link>
      </div>
    </Card>
  );
}
