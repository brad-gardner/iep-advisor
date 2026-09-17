import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { formatDate } from '@/lib/format-date';
import { GoalHistoryDrawer } from './goal-history-drawer';
import { GoalStatusBadge } from './goal-status-badge';
import { GoalStatusDialog } from './goal-status-dialog';
import { GoalTrajectorySparkline } from './goal-trajectory-sparkline';
import { LogProgressForm } from './log-progress-form';
import type { GoalObservationDto, GoalRecordDto } from '../types';

interface GoalCardProps {
  goal: GoalRecordDto;
  /** Required unless `readOnly` — used only by the History drawer's staff read. */
  studentId?: number;
  /** Parent progress view: sparkline + dates only, no logging/status/history actions. */
  readOnly?: boolean;
  onObservationLogged?: (goalRecordId: number, observation: GoalObservationDto) => void;
  onStatusChanged?: (updated: GoalRecordDto) => void;
  'data-testid'?: string;
}

/** One goal's card: status, domain, goal text, baseline/target, last observed
 *  + stale badge, trajectory sparkline, and (staff only) "Log progress" /
 *  status-change / history actions. */
export function GoalCard({
  goal,
  studentId,
  readOnly = false,
  onObservationLogged,
  onStatusChanged,
  'data-testid': testId,
}: GoalCardProps) {
  const [loggingProgress, setLoggingProgress] = useState(false);
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);

  return (
    <Card
      className="space-y-3"
      tabIndex={-1}
      id={`goal-card-${goal.id}`}
      data-testid={testId ?? `goal-card-${goal.id}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          {goal.domain && <p className="text-xs font-medium uppercase tracking-wide text-brand-slate-400">{goal.domain}</p>}
          <h3 className="font-serif text-base text-brand-slate-800">{goal.goalText}</h3>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {goal.isStale && (
            <Badge variant="warning" data-testid={`goal-stale-${goal.id}`}>
              Stale
            </Badge>
          )}
          <GoalStatusBadge status={goal.status} />
        </div>
      </div>

      <dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
        {goal.baseline && (
          <div>
            <dt className="text-[13px] font-medium text-brand-slate-500">Baseline</dt>
            <dd className="text-brand-slate-700">{goal.baseline}</dd>
          </div>
        )}
        {goal.targetCriteria && (
          <div>
            <dt className="text-[13px] font-medium text-brand-slate-500">Target</dt>
            <dd className="text-brand-slate-700">{goal.targetCriteria}</dd>
          </div>
        )}
      </dl>

      {goal.statusReason && (goal.status === 'NotMet' || goal.status === 'Retired') && (
        <Markdown content={goal.statusReason} className="text-xs text-brand-slate-500" />
      )}

      <div>
        <p className="mb-1 text-xs text-brand-slate-500">
          Last observed: {formatDate(goal.lastObservedAt, 'Never')}
        </p>
        <GoalTrajectorySparkline trajectory={goal.trajectory} goalText={goal.goalText} />
      </div>

      {!readOnly && (
        <div className="flex flex-wrap items-center gap-2 border-t border-brand-slate-100 pt-3">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setHistoryOpen(true)}
            data-testid={`goal-history-open-${goal.id}`}
          >
            History
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setLoggingProgress((v) => !v)}
            data-testid={`goal-log-progress-open-${goal.id}`}
          >
            Log progress
          </Button>
          {goal.status !== 'Retired' && goal.status !== 'Carried' && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setStatusDialogOpen(true)}
              data-testid={`goal-status-open-${goal.id}`}
            >
              Change status
            </Button>
          )}
        </div>
      )}

      {!readOnly && loggingProgress && (
        <LogProgressForm
          goalRecordId={goal.id}
          data-testid={`goal-log-progress-form-${goal.id}`}
          onCancel={() => setLoggingProgress(false)}
          onLogged={(observation) => {
            onObservationLogged?.(goal.id, observation);
            setLoggingProgress(false);
          }}
        />
      )}

      {!readOnly && (
        <GoalStatusDialog
          open={statusDialogOpen}
          goal={goal}
          onClose={() => setStatusDialogOpen(false)}
          onChanged={(updated) => onStatusChanged?.(updated)}
        />
      )}

      {!readOnly && studentId != null && (
        <GoalHistoryDrawer
          open={historyOpen}
          onClose={() => setHistoryOpen(false)}
          studentId={studentId}
          lineageId={goal.lineageId}
          goalText={goal.goalText}
        />
      )}
    </Card>
  );
}
