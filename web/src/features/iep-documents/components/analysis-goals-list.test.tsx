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
  it('renders its "Goal Analysis" heading as an h2 by default', () => {
    render(<AnalysisGoalsList goalAnalyses={[makeGoal(1)]} />);
    expect(
      screen.getByRole('heading', { level: 2, name: 'Goal Analysis (1 goals)' })
    ).toBeInTheDocument();
  });

  it('renders the heading as an h3 when nested under another h2 (headingLevel={3})', () => {
    render(<AnalysisGoalsList goalAnalyses={[makeGoal(1)]} headingLevel={3} />);
    expect(
      screen.getByRole('heading', { level: 3, name: 'Goal Analysis (1 goals)' })
    ).toBeInTheDocument();
  });
});
