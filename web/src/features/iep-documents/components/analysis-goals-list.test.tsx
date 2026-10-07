import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { GoalAnalysis, SmartCriterion } from '@/types/api';
import { AnalysisGoalsList } from './analysis-goals-list';

const ok: SmartCriterion = { rating: 'green', explanation: 'fine' };

function makeGoal(goalId: number): GoalAnalysis {
  return {
    goalId,
    goalText: 'Reading goal text',
    domain: 'Reading',
    smartAnalysis: { specific: ok, measurable: ok, achievable: ok, relevant: ok, timeBound: ok },
    overallRating: 'green',
    plainLanguageSummary: 'Looks fine.',
    strengths: [],
    concerns: [],
    suggestedImprovements: [],
  };
}

describe('AnalysisGoalsList', () => {
  it('renders its "Goal Analysis" heading as an h2 by default, with correct singular/plural grammar', () => {
    render(<AnalysisGoalsList goalAnalyses={[makeGoal(1)]} />);
    expect(
      screen.getByRole('heading', { level: 2, name: 'Goal Analysis (1 goal)' })
    ).toBeInTheDocument();
  });

  it('renders the heading as an h3 when nested under another h2 (headingLevel={3})', () => {
    render(<AnalysisGoalsList goalAnalyses={[makeGoal(1)]} headingLevel={3} />);
    expect(
      screen.getByRole('heading', { level: 3, name: 'Goal Analysis (1 goal)' })
    ).toBeInTheDocument();
  });

  it('pluralizes the heading for more than one goal', () => {
    render(<AnalysisGoalsList goalAnalyses={[makeGoal(1), makeGoal(2)]} />);
    expect(
      screen.getByRole('heading', { level: 2, name: 'Goal Analysis (2 goals)' })
    ).toBeInTheDocument();
  });

  it('uses correct singular grammar for the strong/needs-improvement/concerns counts', () => {
    render(<AnalysisGoalsList goalAnalyses={[makeGoal(1)]} />);
    expect(screen.getByText('1 strong')).toBeInTheDocument();
  });
});
