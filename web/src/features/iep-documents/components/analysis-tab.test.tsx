import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { GoalAnalysis, SmartCriterion } from '@/types/api';
import type {
  AnalysisRunLatest,
  AnalysisRunSection,
  AnalysisRunSource,
} from '@/features/analysis/types';
import { AnalysisTab } from './analysis-tab';

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

function makeSource(overrides: Partial<AnalysisRunSource> = {}): AnalysisRunSource {
  return {
    id: 501,
    sourceType: 'IepDocument',
    sourceId: 12,
    sourceLabel: 'spring-iep.pdf',
    status: 'Completed',
    errorMessage: null,
    ...overrides,
  };
}

function makeRun(overrides: Partial<AnalysisRunLatest> = {}): AnalysisRunLatest {
  return {
    id: 7,
    childProfileId: 4,
    status: 'Completed',
    overallSummary: 'Overall summary of the IEP.',
    crossDocSynthesis: null,
    overallRedFlags: [],
    advocacyGapAnalysis: null,
    parentGoalsSnapshot: [],
    sources: [makeSource()],
    sections: [],
    errorMessage: null,
    createdAt: '2026-03-05T00:00:00Z',
    otherSources: [],
    stale: false,
    ...overrides,
  };
}

interface RenderOverrides {
  run?: AnalysisRunLatest | null;
  source?: AnalysisRunSource | null;
  sections?: AnalysisRunSection[];
  goalAnalyses?: GoalAnalysis[] | null;
  otherSources?: AnalysisRunLatest['otherSources'];
  stale?: boolean;
  isLoading?: boolean;
  loadError?: string | null;
  isTriggering?: boolean;
  triggerError?: string | null;
  onTrigger?: () => void;
  onReload?: () => void;
  initialView?: 'overview' | 'goals';
}

function renderTab(overrides: RenderOverrides = {}) {
  const onTrigger = overrides.onTrigger ?? vi.fn();
  const onReload = overrides.onReload ?? vi.fn();
  const props = {
    childId: 4,
    run: overrides.run === undefined ? null : overrides.run,
    source: overrides.source === undefined ? null : overrides.source,
    sections: overrides.sections ?? [],
    goalAnalyses: overrides.goalAnalyses === undefined ? null : overrides.goalAnalyses,
    otherSources: overrides.otherSources ?? [],
    stale: overrides.stale ?? false,
    isLoading: overrides.isLoading ?? false,
    loadError: overrides.loadError ?? null,
    isTriggering: overrides.isTriggering ?? false,
    triggerError: overrides.triggerError ?? null,
    onTrigger,
    onReload,
    initialView: overrides.initialView,
  };
  render(
    <MemoryRouter>
      <AnalysisTab {...props} />
    </MemoryRouter>,
  );
  return { onTrigger, onReload };
}

describe('AnalysisTab', () => {
  it('shows the empty state with an "Analyze this IEP" action when the document has never been analyzed', () => {
    const { onTrigger } = renderTab({ run: null });

    expect(screen.getByText('Analyze Your IEP')).toBeInTheDocument();
    const button = screen.getByTestId('analyze-button');
    fireEvent.click(button);
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it('surfaces a trigger error (e.g. subscription required) on top of the empty state', () => {
    renderTab({ run: null, triggerError: 'Active subscription required' });
    const notice = screen.getByTestId('analysis-trigger-error');
    expect(notice).toHaveTextContent('Active subscription required');
    expect(notice).toHaveAttribute('role', 'alert');
  });

  it('shows a retry notice instead of the Analyze button when the run failed to load (not just "never analyzed")', () => {
    const { onReload } = renderTab({ run: null, loadError: 'Could not load this analysis.' });

    const notice = screen.getByRole('alert');
    expect(notice).toHaveTextContent('Could not load this analysis.');
    const retryButton = within(notice).getByRole('button', { name: 'Try again' });
    expect(screen.queryByTestId('analyze-button')).not.toBeInTheDocument();

    fireEvent.click(retryButton);
    expect(onReload).toHaveBeenCalledTimes(1);
  });

  it('keeps the load-error notice (and its Try again control) mounted instead of swapping to the spinner while a retry is in flight', () => {
    renderTab({ run: null, loadError: 'Could not load this analysis.', isLoading: true });

    const notice = screen.getByRole('alert');
    const retryButton = within(notice).getByRole('button', { name: 'Try again' });
    expect(retryButton).toBeDisabled();
    expect(retryButton).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByText('Loading analysis…')).not.toBeInTheDocument();
  });

  it('keeps showing a previously loaded run alongside a load-error retry notice when a background refresh fails', () => {
    const source = makeSource();
    const run = makeRun({ sources: [source] });
    renderTab({ run, source, loadError: 'Could not load this analysis.' });

    expect(screen.getByTestId('analysis-load-error')).toBeInTheDocument();
    // The previously loaded, completed run is still shown underneath.
    expect(screen.getByText('Overall summary of the IEP.')).toBeInTheDocument();
  });

  it('shows the processing card while the run (or this source) is still in progress', () => {
    const run = makeRun({ status: 'Running', sources: [makeSource({ status: 'Running' })] });
    renderTab({ run, source: makeSource({ status: 'Running' }) });

    expect(screen.getByText('Analyzing Your IEP')).toBeInTheDocument();
    expect(screen.getByText(/This usually takes a few minutes\./)).toBeInTheDocument();
  });

  it('shows goal ratings for a completed analysis', () => {
    const source = makeSource();
    const goals = [makeGoal(340), makeGoal(341)];
    // The hook excludes the iep_goals section from `sections` (its `analysis`
    // is always null) and surfaces its payload separately as `goalAnalyses`.
    const run = makeRun({ sources: [source] });
    renderTab({ run, source, goalAnalyses: goals, initialView: 'goals' });

    expect(screen.getByText('Goal Analysis (2 goals)')).toBeInTheDocument();
    expect(screen.getByTestId('analysis-goal-340')).toBeInTheDocument();
    expect(screen.getByTestId('analysis-goal-341')).toBeInTheDocument();
  });

  it('shows an info line linking to the full run when this document was analyzed with other documents', () => {
    const source = makeSource();
    const run = makeRun({
      sources: [source],
      otherSources: [{ sourceType: 'EtrDocument', sourceId: 55, label: 'ETR, Sep 2025' }],
    });
    renderTab({ run, source, otherSources: run.otherSources });

    const notice = screen.getByTestId('analysis-multi-source-info');
    expect(notice).toHaveTextContent('Part of an analysis with ETR, Sep 2025');
    const link = screen.getByRole('link', { name: 'View full analysis →' });
    expect(link).toHaveAttribute('href', `/children/4/analysis?run=${run.id}`);
  });

  it('shows a failed-source notice with a retry action when this document could not be analyzed', () => {
    const source = makeSource({ status: 'Error', errorMessage: 'The document could not be parsed.' });
    const run = makeRun({ sources: [source] });
    const { onTrigger } = renderTab({ run, source });

    expect(screen.getByText("Couldn't analyze this document")).toBeInTheDocument();
    expect(screen.getByText('The document could not be parsed.')).toBeInTheDocument();
    const button = screen.getByTestId('analyze-button');
    fireEvent.click(button);
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it('shows a stale banner with a re-analyze action when the document moved on since this run', () => {
    const source = makeSource();
    const run = makeRun({ sources: [source], stale: true });
    const { onTrigger } = renderTab({ run, source, stale: true });

    const banner = screen.getByTestId('analysis-stale-banner');
    expect(banner).toHaveTextContent(
      'This analysis was made before the IEP was last updated. Re-analyze to refresh it.',
    );
    fireEvent.click(screen.getByTestId('reanalyze-button'));
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it('shows a run-level error state with a retry action', () => {
    const run = makeRun({ status: 'Error', errorMessage: 'The analysis could not be completed.' });
    const { onTrigger } = renderTab({ run, source: makeSource({ status: 'Error' }) });

    expect(screen.getByText('Analysis Failed')).toBeInTheDocument();
    expect(screen.getByText('The analysis could not be completed.')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('analyze-button'));
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });
});
