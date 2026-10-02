import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type {
  AnalysisRunLatest,
  AnalysisRunSection,
  AnalysisRunSource,
  EtrCompletenessPayload,
  EtrEligibilityPayload,
} from '@/features/analysis/types';
import { EtrAnalysisTab } from './etr-analysis-tab';

function makeSource(overrides: Partial<AnalysisRunSource> = {}): AnalysisRunSource {
  return {
    id: 501,
    sourceType: 'EtrDocument',
    sourceId: 12,
    sourceLabel: 'etr-spring-2026.pdf',
    status: 'Completed',
    errorMessage: null,
    ...overrides,
  };
}

function makeCompleteness(
  overrides: Partial<EtrCompletenessPayload> = {},
): EtrCompletenessPayload {
  return {
    evaluatedDomains: [
      {
        domain: 'Cognitive',
        toolsUsed: ['WISC-V'],
        adequacyRating: 'adequate',
        notes: null,
      },
    ],
    missingDomains: [{ domain: 'Adaptive Behavior', rationale: 'Not assessed.' }],
    overallCompletenessRating: 'thin',
    ...overrides,
  };
}

function makeEligibility(overrides: Partial<EtrEligibilityPayload> = {}): EtrEligibilityPayload {
  return {
    statedCategory: 'Specific Learning Disability',
    statedConclusion: 'Eligible for special education.',
    dataSupportsConclusion: true,
    supportingEvidence: ['Standardized testing shows a significant discrepancy.'],
    contradictingEvidence: [],
    alternativeConsiderations: [],
    notes: null,
    ...overrides,
  };
}

function makeRun(overrides: Partial<AnalysisRunLatest> = {}): AnalysisRunLatest {
  return {
    id: 9,
    childProfileId: 4,
    status: 'Completed',
    overallSummary: 'Overall summary of the ETR.',
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
  completeness?: EtrCompletenessPayload | null;
  eligibility?: EtrEligibilityPayload | null;
  otherSources?: AnalysisRunLatest['otherSources'];
  stale?: boolean;
  isLoading?: boolean;
  loadError?: string | null;
  isTriggering?: boolean;
  triggerError?: string | null;
  onTrigger?: () => void;
  onReload?: () => void;
}

function renderTab(overrides: RenderOverrides = {}) {
  const onTrigger = overrides.onTrigger ?? vi.fn();
  const onReload = overrides.onReload ?? vi.fn();
  const props = {
    childId: 4,
    run: overrides.run === undefined ? null : overrides.run,
    source: overrides.source === undefined ? null : overrides.source,
    sections: overrides.sections ?? [],
    completeness: overrides.completeness === undefined ? null : overrides.completeness,
    eligibility: overrides.eligibility === undefined ? null : overrides.eligibility,
    otherSources: overrides.otherSources ?? [],
    stale: overrides.stale ?? false,
    isLoading: overrides.isLoading ?? false,
    loadError: overrides.loadError ?? null,
    isTriggering: overrides.isTriggering ?? false,
    triggerError: overrides.triggerError ?? null,
    onTrigger,
    onReload,
  };
  render(
    <MemoryRouter>
      <EtrAnalysisTab {...props} />
    </MemoryRouter>,
  );
  return { onTrigger, onReload };
}

describe('EtrAnalysisTab', () => {
  it('shows the empty state with an "Analyze ETR" action when the document has never been analyzed', () => {
    const { onTrigger } = renderTab({ run: null });

    expect(screen.getByText('Analyze this ETR')).toBeInTheDocument();
    const button = screen.getByTestId('etr-analyze-button');
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
    expect(screen.queryByTestId('etr-analyze-button')).not.toBeInTheDocument();

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

  it('shows the processing card while the run (or this source) is still in progress', () => {
    const run = makeRun({ status: 'Running', sources: [makeSource({ status: 'Running' })] });
    renderTab({ run, source: makeSource({ status: 'Running' }) });

    expect(screen.getByText('Analyzing Your ETR')).toBeInTheDocument();
    expect(screen.getByText(/This usually takes a few minutes\./)).toBeInTheDocument();
  });

  it('shows completeness and eligibility for a completed analysis', () => {
    const source = makeSource();
    const completeness = makeCompleteness();
    const eligibility = makeEligibility();
    const run = makeRun({ sources: [source] });
    renderTab({ run, source, completeness, eligibility });

    // Overview is the default view.
    expect(screen.getByTestId('etr-analysis-overview')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('etr-analysis-nav-completeness'));
    expect(screen.getByTestId('etr-assessment-completeness')).toBeInTheDocument();
    expect(screen.getByText('Cognitive')).toBeInTheDocument();
    expect(screen.getByText('Adaptive Behavior')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('etr-analysis-nav-eligibility'));
    expect(screen.getByTestId('etr-eligibility-review')).toBeInTheDocument();
    expect(screen.getByText('Specific Learning Disability')).toBeInTheDocument();
  });

  it('shows an info line linking to the full run when this ETR was analyzed with other documents', () => {
    const source = makeSource();
    const run = makeRun({
      sources: [source],
      otherSources: [{ sourceType: 'IepDocument', sourceId: 35, label: 'IEP, Mar 2026' }],
    });
    renderTab({ run, source, otherSources: run.otherSources });

    const notice = screen.getByTestId('analysis-multi-source-info');
    expect(notice).toHaveTextContent('Part of an analysis with IEP, Mar 2026');
    const link = screen.getByRole('link', { name: 'View full analysis →' });
    expect(link).toHaveAttribute('href', `/children/4/analysis?run=${run.id}`);
  });

  it('shows a failed-source notice with a retry action when this ETR could not be analyzed', () => {
    const source = makeSource({ status: 'Error', errorMessage: 'The document could not be parsed.' });
    const run = makeRun({ sources: [source] });
    const { onTrigger } = renderTab({ run, source });

    expect(screen.getByText("Couldn't analyze this document")).toBeInTheDocument();
    expect(screen.getByText('The document could not be parsed.')).toBeInTheDocument();
    const button = screen.getByTestId('etr-analyze-button');
    fireEvent.click(button);
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it('shows a stale banner with a re-analyze action when the ETR moved on since this run', () => {
    const source = makeSource();
    const run = makeRun({ sources: [source], stale: true });
    const { onTrigger } = renderTab({ run, source, stale: true });

    const banner = screen.getByTestId('analysis-stale-banner');
    expect(banner).toHaveTextContent(
      'This analysis was made before the ETR was last updated. Re-analyze to refresh it.',
    );
    fireEvent.click(screen.getByTestId('reanalyze-button'));
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it('shows a run-level error state with a retry action', () => {
    const run = makeRun({ status: 'Error', errorMessage: 'The analysis could not be completed.' });
    const { onTrigger } = renderTab({ run, source: makeSource({ status: 'Error' }) });

    expect(screen.getByText('Analysis Failed')).toBeInTheDocument();
    expect(screen.getByText('The analysis could not be completed.')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('etr-analyze-button'));
    expect(onTrigger).toHaveBeenCalledTimes(1);
  });
});
