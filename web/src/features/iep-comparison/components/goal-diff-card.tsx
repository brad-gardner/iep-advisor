import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { GoalDiff, ModifiedGoalDiff } from '@/types/api';

const FIELD_LABELS: Record<string, string> = {
  TargetCriteria: 'Target Criteria',
  Baseline: 'Baseline',
  MeasurementMethod: 'Measurement Method',
  Timeframe: 'Timeframe',
  GoalText: 'Goal Text',
};

function AddedGoalCard({ goal }: { goal: GoalDiff }) {
  return (
    <Card className="border-l-4 border-l-brand-teal-500">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          {goal.domain && (
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold mb-1">
              {goal.domain}
            </p>
          )}
          <p className="text-sm text-brand-slate-700">{goal.goalText}</p>
        </div>
        <Badge variant="success">Added</Badge>
      </div>
    </Card>
  );
}

function RemovedGoalCard({ goal }: { goal: GoalDiff }) {
  return (
    <Card className="border-l-4 border-l-brand-red">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          {goal.domain && (
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold mb-1">
              {goal.domain}
            </p>
          )}
          <p className="text-sm text-brand-slate-700 line-through opacity-70">{goal.goalText}</p>
        </div>
        <Badge variant="error">Removed</Badge>
      </div>
    </Card>
  );
}

function ModifiedGoalCard({ goal }: { goal: ModifiedGoalDiff }) {
  return (
    <Card className="border-l-4 border-l-brand-amber-400">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className="flex-1 min-w-0">
          {goal.domain && (
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold mb-1">
              {goal.domain}
            </p>
          )}
          <p className="text-sm text-brand-slate-700">{goal.newerGoalText}</p>
        </div>
        <Badge variant="warning">Modified</Badge>
      </div>

      {goal.changes.length > 0 && (
        <div className="space-y-2 pt-3 border-t border-brand-slate-100">
          <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
            Changes
          </p>
          {goal.changes.map((change, i) => (
            <div key={i} className="flex items-baseline gap-2 text-sm">
              <span className="text-brand-slate-500 font-medium shrink-0">
                {FIELD_LABELS[change.field] || change.field}:
              </span>
              <span className="text-brand-slate-400 line-through">
                {change.older || '(none)'}
              </span>
              <span className="text-brand-slate-400" aria-hidden="true">
                &rarr;
              </span>
              <span className="text-brand-slate-700 font-medium">
                {change.newer || '(none)'}
              </span>
            </div>
          ))}
        </div>
      )}
    </Card>
  );
}

export function GoalDiffCard({
  type,
  goal,
}: {
  type: 'added' | 'removed' | 'modified';
  goal: GoalDiff | ModifiedGoalDiff;
}) {
  switch (type) {
    case 'added':
      return <AddedGoalCard goal={goal as GoalDiff} />;
    case 'removed':
      return <RemovedGoalCard goal={goal as GoalDiff} />;
    case 'modified':
      return <ModifiedGoalCard goal={goal as ModifiedGoalDiff} />;
  }
}
