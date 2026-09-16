import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Target } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { useStudentGoals } from '../hooks/use-student-goals';
import { GoalCard } from './goal-card';

interface GoalsCardProps {
  studentId: number;
}

/** Student page "Goals" card: one card per active goal lineage, with a
 *  `?goal={id}` deep link (from the home obligation row / a notification)
 *  that scrolls to and focuses the matching card once it has loaded. */
export function GoalsCard({ studentId }: GoalsCardProps) {
  const { goals, isLoading, error, retry, applyObservation, applyStatusChange } = useStudentGoals(studentId);
  const [searchParams] = useSearchParams();
  const focusGoalId = searchParams.get('goal');

  // Scroll/focus the deep-linked card exactly once, when it first exists. `goals` is a fresh
  // array after every log/status update, so keying only on it would yank focus back to the
  // linked card each time the user touches any goal.
  const focusedGoalIdRef = useRef<string | null>(null);
  useEffect(() => {
    if (!focusGoalId || !goals || focusedGoalIdRef.current === focusGoalId) return;
    const el = document.getElementById(`goal-card-${focusGoalId}`);
    if (!el) return;
    el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    el.focus({ preventScroll: true });
    focusedGoalIdRef.current = focusGoalId;
  }, [focusGoalId, goals]);

  return (
    <Card data-testid="goals-card">
      <h2 className="mb-4 font-serif text-lg text-brand-slate-800">Goals</h2>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={retry} data-testid="goals-card-retry">
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {!error && isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      )}

      {!error && !isLoading && goals && goals.length === 0 && (
        <EmptyState
          icon={Target}
          title="No goals yet"
          description="Goals appear here once a finalized IEP or ETR includes them."
        />
      )}

      {!error && !isLoading && goals && goals.length > 0 && (
        <div className="space-y-4" data-testid="goals-card-list">
          {goals.map((goal) => (
            <GoalCard
              key={goal.id}
              goal={goal}
              studentId={studentId}
              onObservationLogged={applyObservation}
              onStatusChanged={applyStatusChange}
            />
          ))}
        </div>
      )}
    </Card>
  );
}
