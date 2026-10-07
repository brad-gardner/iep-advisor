import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Drawer } from '@/components/ui/drawer';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDate } from '@/lib/format-date';
import { type LoadError, toLoadError, loadErrorText } from '@/lib/api-error';
import { getStudentGoalHistory } from '../api/goals-api';
import { GoalStatusBadge } from './goal-status-badge';
import type { GoalRecordDto } from '../types';

interface GoalHistoryDrawerProps {
  open: boolean;
  onClose: () => void;
  studentId: number;
  lineageId: string;
  goalText: string;
}

/** One goal lineage's full record history across finalizes/amendments —
 *  every prior "version" of the same goal row (Carried/Retired/Met/NotMet),
 *  newest first. Loaded lazily on open. */

export function GoalHistoryDrawer({ open, onClose, studentId, lineageId, goalText }: GoalHistoryDrawerProps) {
  const { t } = useTranslation('goals');
  const [records, setRecords] = useState<GoalRecordDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);

  // Reset to a fresh loading state exactly when the drawer transitions to
  // open — adjusted during render (the idiom `useSharedDraftDetail` uses for
  // its own reset), not as a synchronous setState at the top of the effect —
  // so a reopen always shows a fresh fetch rather than stale prior history.
  const [seenOpen, setSeenOpen] = useState(false);
  if (open !== seenOpen) {
    setSeenOpen(open);
    if (open) {
      setRecords(null);
      setError(null);
    }
  }

  useEffect(() => {
    if (!open) return;
    let active = true;
    (async () => {
      try {
        const res = await getStudentGoalHistory(studentId);
        if (!active) return;
        if (res.success && res.data) {
          const lineage = res.data.find((l) => l.lineageId === lineageId);
          setRecords(lineage?.records ?? []);
          setError(null);
        } else {
          setError(toLoadError(res));
        }
      } catch (err) {
        if (!active) return;
        setError(toLoadError(err));
      }
    })();
    return () => {
      active = false;
    };
  }, [open, studentId, lineageId]);

  const errorMessage = loadErrorText(error, t('historyDrawer.loadError'));

  return (
    <Drawer open={open} onClose={onClose} title={t('historyDrawer.title')} data-testid="goal-history-drawer">
      <p className="mb-4 text-sm text-brand-slate-600">{goalText}</p>

      {errorMessage && (
        <div role="alert">
          <Notice variant="error" title={errorMessage} />
        </div>
      )}

      {!error && records === null && (
        <div className="space-y-2" aria-busy="true">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      )}

      {!error && records !== null && records.length === 0 && (
        <p className="text-sm text-brand-slate-500">{t('historyDrawer.noHistory')}</p>
      )}

      {!error && records !== null && records.length > 0 && (
        <ol className="space-y-3" data-testid="goal-history-list">
          {records.map((r) => (
            <li
              key={r.id}
              className="rounded-card border border-brand-slate-200 p-3"
              data-testid={`goal-history-record-${r.id}`}
            >
              <div className="mb-1 flex items-center justify-between gap-2">
                <span className="text-xs font-medium text-brand-slate-500">
                  {t('historyDrawer.versionLabel', { number: r.versionNumber, date: formatDate(r.projectedAt) })}
                </span>
                <GoalStatusBadge status={r.status} />
              </div>
              <p className="text-sm text-brand-slate-800">{r.goalText}</p>
              {r.statusReason && (
                <Markdown content={r.statusReason} className="mt-1 text-xs text-brand-slate-500" />
              )}
            </li>
          ))}
        </ol>
      )}
    </Drawer>
  );
}
