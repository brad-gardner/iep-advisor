import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { formatDate } from '@/lib/format-date';
import { orgRoleLabel } from '@/lib/org-role-label';
import type { DashboardInvite } from '../types';

interface DashboardInvitesTileProps {
  // Pending + expired invites, expired-first (server-ordered).
  invites: DashboardInvite[];
}

// Invites needing attention (pending + expired, expired flagged). Presentational:
// the composing container owns the single dashboard fetch.
export function DashboardInvitesTile({ invites }: DashboardInvitesTileProps) {
  const { t } = useTranslation('district-admin');
  return (
    <Card data-testid="dashboard-invites-tile">
      <h2 className="font-serif text-xl mb-4">{t('dashboard.invitesTile.title')}</h2>

      {invites.length === 0 ? (
        <p
          className="text-sm text-brand-slate-500"
          data-testid="dashboard-invites-tile-empty"
        >
          {t('dashboard.invitesTile.empty')}
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
                  {isExpired && <Badge variant="error">{t('dashboard.invitesTile.expiredBadge')}</Badge>}
                </div>
                <p className="text-xs text-brand-slate-500">
                  {orgRoleLabel(invite.orgRoleName)}
                  {invite.schoolName ? ` · ${invite.schoolName}` : ` · ${t('dashboard.invitesTile.districtWide')}`}
                  {' · '}
                  {isExpired ? t('dashboard.invitesTile.expiredOn') : t('dashboard.invitesTile.expires')}{' '}
                  {formatDate(invite.inviteExpiresAt, '')}
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
          {t('dashboard.invitesTile.manageLink')}
        </Link>
      </div>
    </Card>
  );
}
