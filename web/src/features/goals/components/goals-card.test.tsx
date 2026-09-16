import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import type { GoalRecordDto } from '../types';

const goalsApi = vi.hoisted(() => ({
  getStudentGoals: vi.fn(),
  addGoalObservation: vi.fn(),
}));
vi.mock('../api/goals-api', () => goalsApi);

import { GoalsCard } from './goals-card';

function makeGoal(overrides: Partial<GoalRecordDto> = {}): GoalRecordDto {
  return {
    id: 1,
    lineageId: 'lineage-1',
    versionId: 10,
    versionNumber: 1,
    documentTypeKey: 'IEP',
    domain: 'Reading',
    goalText: 'Read grade-level text with 90% accuracy',
    baseline: 'Reads 60 wpm',
    targetCriteria: 'Reads 100 wpm',
    measurementMethod: 'Curriculum-based measurement',
    timeframe: 'By annual review',
    status: 'Active',
    statusReason: null,
    reviewedAt: null,
    projectedAt: '2026-01-01T00:00:00.000Z',
    lastObservedAt: null,
    staleAfterDays: 45,
    isStale: false,
    observations: [],
    trajectory: { points: [], insufficientData: true },
    ...overrides,
  };
}

function renderCard(studentId = 5) {
  return render(
    <ToastProvider>
      <MemoryRouter initialEntries={[`/educator/students/${studentId}`]}>
        <GoalsCard studentId={studentId} />
      </MemoryRouter>
    </ToastProvider>
  );
}

describe('GoalsCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders a goal card with status, domain, goal text, baseline/target, and stale badge', async () => {
    goalsApi.getStudentGoals.mockResolvedValue({
      success: true,
      data: [makeGoal({ isStale: true, trajectory: { points: [{ observedAt: '2026-01-01', value: 60 }, { observedAt: '2026-02-01', value: 70 }], insufficientData: false } })],
    });

    renderCard();

    expect(await screen.findByText('Read grade-level text with 90% accuracy')).toBeInTheDocument();
    expect(screen.getByText('Reading')).toBeInTheDocument();
    expect(screen.getByText('Reads 60 wpm')).toBeInTheDocument();
    expect(screen.getByText('Reads 100 wpm')).toBeInTheDocument();
    expect(screen.getByTestId('goal-status-Active')).toBeInTheDocument();
    expect(screen.getByTestId('goal-stale-1')).toBeInTheDocument();
    expect(screen.getByTestId('goal-trajectory-sparkline')).toBeInTheDocument();
  });

  it('shows "Insufficient data" honestly when the trajectory has fewer than two points', async () => {
    goalsApi.getStudentGoals.mockResolvedValue({
      success: true,
      data: [makeGoal({ trajectory: { points: [], insufficientData: true } })],
    });

    renderCard();

    expect(await screen.findByTestId('goal-trajectory-insufficient')).toHaveTextContent('Insufficient data');
    expect(screen.queryByTestId('goal-trajectory-sparkline')).not.toBeInTheDocument();
  });

  it('logs progress: posts the observation and optimistically appends it to the trajectory', async () => {
    goalsApi.getStudentGoals.mockResolvedValue({
      success: true,
      data: [makeGoal({ trajectory: { points: [{ observedAt: '2026-01-01T00:00:00.000Z', value: 60 }], insufficientData: true } })],
    });
    goalsApi.addGoalObservation.mockResolvedValue({
      success: true,
      data: {
        id: 99,
        goalRecordId: 1,
        observedAt: '2026-02-01T00:00:00.000Z',
        value: 75,
        unit: 'wpm',
        note: null,
        recordedByUserId: 1,
      },
    });

    renderCard();
    await screen.findByText('Read grade-level text with 90% accuracy');

    fireEvent.click(screen.getByTestId('goal-log-progress-open-1'));
    fireEvent.change(screen.getByTestId('goal-log-progress-form-1-value'), { target: { value: '75' } });
    fireEvent.change(screen.getByTestId('goal-log-progress-form-1-unit'), { target: { value: 'wpm' } });
    fireEvent.click(screen.getByTestId('goal-log-progress-form-1-submit'));

    await waitFor(() =>
      expect(goalsApi.addGoalObservation).toHaveBeenCalledWith(1, { value: 75, unit: 'wpm', note: undefined })
    );

    // Optimistic append: the goal now has 2 numeric points, so the sparkline
    // replaces the "insufficient data" notice, and the form closes.
    await waitFor(() => expect(screen.getByTestId('goal-trajectory-sparkline')).toBeInTheDocument());
    expect(screen.queryByTestId('goal-trajectory-insufficient')).not.toBeInTheDocument();
    expect(screen.queryByTestId('goal-log-progress-form-1')).not.toBeInTheDocument();
  });
});
