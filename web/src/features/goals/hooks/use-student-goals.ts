import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getStudentGoals } from '../api/goals-api';
import type { GoalObservationDto, GoalRecordDto, GoalTrajectoryDto } from '../types';

interface UseStudentGoalsResult {
  goals: GoalRecordDto[] | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  /** Optimistically append a freshly logged observation to its goal card. */
  applyObservation: (goalRecordId: number, observation: GoalObservationDto) => void;
  /** Replace a goal record after a status change. */
  applyStatusChange: (updated: GoalRecordDto) => void;
}

function appendTrajectoryPoint(trajectory: GoalTrajectoryDto, observation: GoalObservationDto): GoalTrajectoryDto {
  if (observation.value == null) return trajectory;
  const points = [...trajectory.points, { observedAt: observation.observedAt, value: observation.value }];
  return { points, insufficientData: points.length < 2 };
}

/** A student's current goal records (one per active lineage) for the "Goals"
 *  card on the educator student page. */
export function useStudentGoals(studentId: number): UseStudentGoalsResult {
  const [goals, setGoals] = useState<GoalRecordDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    if (!studentId) return;
    let active = true;
    (async () => {
      try {
        const res = await getStudentGoals(studentId);
        if (!active) return;
        if (res.success && res.data) {
          setGoals(res.data);
          setError(null);
        } else {
          setError(res.message ?? 'Could not load goals.');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load goals.'));
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId, retryToken]);

  const applyObservation = useCallback((goalRecordId: number, observation: GoalObservationDto) => {
    setGoals((current) =>
      current
        ? current.map((g) =>
            g.id === goalRecordId
              ? {
                  ...g,
                  observations: [...g.observations, observation].slice(-12),
                  lastObservedAt: observation.observedAt,
                  isStale: false,
                  trajectory: appendTrajectoryPoint(g.trajectory, observation),
                }
              : g
          )
        : current
    );
  }, []);

  const applyStatusChange = useCallback((updated: GoalRecordDto) => {
    setGoals((current) => (current ? current.map((g) => (g.id === updated.id ? updated : g)) : current));
  }, []);

  return {
    goals,
    isLoading: goals === null && error === null,
    error,
    retry: () => setRetryToken((t) => t + 1),
    applyObservation,
    applyStatusChange,
  };
}
