import { afterEach, describe, expect, it, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
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

describe('GoalsCard in Spanish', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });
  afterEach(() => resetTestLanguage());

  it('renders the heading, status badge and actions in Spanish', async () => {
    goalsApi.getStudentGoals.mockResolvedValue({ success: true, data: [makeGoal({ isStale: true })] });

    await renderInSpanish(
      <ToastProvider>
        <MemoryRouter initialEntries={['/educator/students/5']}>
          <GoalsCard studentId={5} />
        </MemoryRouter>
      </ToastProvider>
    );

    expect(await screen.findByText('Read grade-level text with 90% accuracy')).toBeInTheDocument();
    expect(screen.getByText('Activa')).toBeInTheDocument();
    expect(screen.getByText('Desactualizada')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Historial' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar progreso' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cambiar estado' })).toBeInTheDocument();
  });

  it('renders the empty state in Spanish', async () => {
    goalsApi.getStudentGoals.mockResolvedValue({ success: true, data: [] });

    await renderInSpanish(
      <ToastProvider>
        <MemoryRouter initialEntries={['/educator/students/5']}>
          <GoalsCard studentId={5} />
        </MemoryRouter>
      </ToastProvider>
    );

    expect(await screen.findByText('Aún no hay metas')).toBeInTheDocument();
  });
});
