import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Trash2 } from 'lucide-react';
import type { ChildAccessEntry } from '@/types/api';
import { getAccessList, revokeAccess } from '../api/sharing-api';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { sharingRoleLabel } from '../lib/role-label';

interface AccessListProps {
  childId: number;
  isOwner: boolean;
}

export function AccessList({ childId, isOwner }: AccessListProps) {
  const { t } = useTranslation('sharing');
  const [entries, setEntries] = useState<ChildAccessEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [revokingId, setRevokingId] = useState<number | null>(null);
  const [isRevoking, setIsRevoking] = useState(false);
  const { show } = useToast();

  const load = useCallback(async () => {
    try {
      const response = await getAccessList(childId);
      if (response.success && response.data) {
        setEntries(response.data);
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsLoading(false);
    }
  }, [childId]);

  useEffect(() => {
    load();
  }, [load]);

  const confirmRevoke = async () => {
    if (revokingId === null) return;
    setIsRevoking(true);
    try {
      const response = await revokeAccess(childId, revokingId);
      if (response.success) {
        setRevokingId(null);
        await load();
        show({ message: t('accessList.revokedToast'), variant: 'success' });
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsRevoking(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <Spinner size="sm" />
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <p className="text-sm text-brand-slate-500 py-2">
        {t('accessList.empty')}
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {entries.map((entry) => {
        const displayName = entry.userName || entry.userEmail || entry.inviteEmail || t('accessList.unknownName');
        const displayEmail = entry.userEmail || entry.inviteEmail;
        const isEntryOwner = entry.role === 'owner';

        return (
          <div
            key={entry.id}
            className="flex items-center justify-between bg-white rounded-card p-3 border border-brand-slate-200"
          >
            <div className="flex items-center gap-3 min-w-0">
              <div className="min-w-0">
                <p className="text-sm font-medium text-brand-slate-800 truncate">
                  {displayName}
                </p>
                {displayEmail && displayName !== displayEmail && (
                  <p className="text-xs text-brand-slate-500 truncate">{displayEmail}</p>
                )}
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              {entry.isPending && (
                <Badge variant="warning">{t('accessList.pendingBadge')}</Badge>
              )}
              {isEntryOwner ? (
                <Badge variant="success">{sharingRoleLabel(entry.role)}</Badge>
              ) : (
                <Badge variant="neutral">
                  {sharingRoleLabel(entry.role)}
                </Badge>
              )}
              {isOwner && !isEntryOwner && (
                <Button
                  variant="danger"
                  className="!px-2 !py-1"
                  onClick={() => setRevokingId(entry.id)}
                  aria-label={t('accessList.revokeAriaLabel', { name: displayName })}
                  data-testid="revoke-access"
                >
                  <Trash2 className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
                </Button>
              )}
            </div>
          </div>
        );
      })}

      <ConfirmDialog
        open={revokingId !== null}
        title={t('accessList.revokeDialogTitle')}
        message={t('accessList.revokeDialogMessage')}
        confirmLabel={t('accessList.revokeDialogTitle')}
        loading={isRevoking}
        onConfirm={confirmRevoke}
        onCancel={() => setRevokingId(null)}
        data-testid="revoke-access-dialog"
      />
    </div>
  );
}
