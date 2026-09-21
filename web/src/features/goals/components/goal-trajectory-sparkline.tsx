import { Sparkline } from '@/components/ui/charts/sparkline';
import { formatDate } from '@/lib/format-date';
import type { GoalTrajectoryDto } from '../types';

/** A goal's progress trajectory: the plan 5 `Sparkline` primitive over the
 *  last 12 numeric observations, or an honest "Insufficient data" notice when
 *  fewer than 2 points exist (the server's own `insufficientData` rule —
 *  never inferred client-side from an empty-looking series). */
export function GoalTrajectorySparkline({ trajectory, goalText }: { trajectory: GoalTrajectoryDto; goalText: string }) {
  if (trajectory.insufficientData) {
    return (
      <p className="text-xs text-brand-slate-500" data-testid="goal-trajectory-insufficient">
        Insufficient data — log at least two observations to see a trend.
      </p>
    );
  }

  return (
    <Sparkline
      title={`Progress trend for ${goalText}`}
      values={trajectory.points.map((p) => p.value)}
      pointLabels={trajectory.points.map((p) => formatDate(p.observedAt))}
      data-testid="goal-trajectory-sparkline"
    />
  );
}
