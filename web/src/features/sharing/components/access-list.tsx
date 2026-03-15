import { useCallback, useEffect, useState } from 'react';
import { Trash2 } from 'lucide-react';
import type { ChildAccessEntry } from '@/types/api';
import { getAccessList, revokeAccess } from '../api/sharing-api';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

interface AccessListProps {
  childId: number;
  isOwner: boolean;
}

export function AccessList({ childId, isOwner }: AccessListProps) {
  const [entries, setEntries] = useState<ChildAccessEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);

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

  const handleRevoke = async (accessId: number) => {
    if (!confirm('Are you sure you want to revoke this access?')) return;
    try {
      const response = await revokeAccess(childId, accessId);
      if (response.success) {
        load();
      }
    } catch {
      // handled by interceptor
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <p className="text-sm text-brand-slate-400 py-2">
        No one else has access to this profile.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {entries.map((entry) => {
        const displayName = entry.userName || entry.userEmail || entry.inviteEmail || 'Unknown';
        const displayEmail = entry.userEmail || entry.inviteEmail;
        const isEntryOwner = entry.role === 'owner';

        return (
          <div
            key={entry.id}
            className="flex items-center justify-between bg-white rounded-card p-3 border-[0.5px] border-brand-slate-200"
          >
            <div className="flex items-center gap-3 min-w-0">
              <div className="min-w-0">
                <p className="text-sm font-medium text-brand-slate-800 truncate">
                  {displayName}
                </p>
                {displayEmail && displayName !== displayEmail && (
                  <p className="text-xs text-brand-slate-400 truncate">{displayEmail}</p>
                )}
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              {entry.isPending && (
                <Badge variant="warning">Pending</Badge>
              )}
              {isEntryOwner ? (
                <Badge variant="success">Owner</Badge>
              ) : (
                <Badge variant="neutral">
                  {entry.role.charAt(0).toUpperCase() + entry.role.slice(1)}
                </Badge>
              )}
              {isOwner && !isEntryOwner && (
                <Button
                  variant="danger"
                  className="!px-2 !py-1"
                  onClick={() => handleRevoke(entry.id)}
                >
                  <Trash2 className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
                </Button>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
