import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { GoalRecordDto } from '../types';

const goalsApi = vi.hoisted(() => ({
  getChildGoals: vi.fn(),
}));
vi.mock('../api/goals-api', () => goalsApi);

import { ChildProgressCard } from './child-progress-card';

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
    lastObservedAt: '2026-02-01T00:00:00.000Z',
    staleAfterDays: 45,
    isStale: false,
    observations: [],
    trajectory: {
      points: [
        { observedAt: '2026-01-01', value: 60 },
        { observedAt: '2026-02-01', value: 70 },
      ],
      insufficientData: false,
    },
    ...overrides,
  };
}

describe('ChildProgressCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing while loading and when there are no goals', async () => {
    goalsApi.getChildGoals.mockResolvedValue({ success: true, data: [] });
    const { container } = render(<ChildProgressCard childId={3} />);
    expect(container).toBeEmptyDOMElement(); // loading

    await waitFor(() => expect(goalsApi.getChildGoals).toHaveBeenCalledWith(3));
    await waitFor(() => expect(container).toBeEmptyDOMElement()); // loaded, empty
  });

  it('renders read-only goal cards: no log-progress, status-change, or history actions', async () => {
    goalsApi.getChildGoals.mockResolvedValue({ success: true, data: [makeGoal()] });

    render(<ChildProgressCard childId={3} />);

    expect(await screen.findByText('Read grade-level text with 90% accuracy')).toBeInTheDocument();
    expect(screen.getByTestId('goal-trajectory-sparkline')).toBeInTheDocument();
    expect(screen.queryByTestId('goal-log-progress-open-1')).not.toBeInTheDocument();
    expect(screen.queryByTestId('goal-status-open-1')).not.toBeInTheDocument();
    expect(screen.queryByTestId('goal-history-open-1')).not.toBeInTheDocument();
  });

  it('renders nothing when the child has no school link (the goals call fails)', async () => {
    goalsApi.getChildGoals.mockRejectedValue(new Error('403'));
    const { container } = render(<ChildProgressCard childId={3} />);
    await waitFor(() => expect(goalsApi.getChildGoals).toHaveBeenCalledWith(3));
    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });
});
