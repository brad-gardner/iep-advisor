import { Card } from '@/components/ui/card';
import { useChildGoals } from '../hooks/use-child-goals';
import { GoalCard } from './goal-card';

interface ChildProgressCardProps {
  childId: number;
}

/** Parent child-overview "Progress" card: the same goal cards as the
 *  educator's Goals card, read-only (no logging/status/history actions).
 *  Renders nothing while loading, on failure, or when there are no goals —
 *  matching `SharedDraftsCard`'s empty-safe idiom (an unlinked child simply
 *  shows no progress card rather than an error). */
export function ChildProgressCard({ childId }: ChildProgressCardProps) {
  const goals = useChildGoals(childId);

  if (goals === null || goals.length === 0) return null;

  return (
    <Card data-testid="child-progress-card">
      <h2 className="mb-1 font-serif">Progress</h2>
      <p className="mb-4 text-sm text-brand-slate-400">
        Goal progress your child's school has logged.
      </p>
      <div className="space-y-4" data-testid="child-progress-card-list">
        {goals.map((goal) => (
          <GoalCard key={goal.id} goal={goal} readOnly data-testid={`child-progress-goal-${goal.id}`} />
        ))}
      </div>
    </Card>
  );
}
