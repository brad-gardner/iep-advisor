import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { RedFlag } from '@/types/api';
import type {
  AnalysisRunLatest,
  AnalysisRunOtherSource,
  AnalysisRunSection,
  AnalysisRunSource,
  EtrCompletenessPayload,
  EtrEligibilityPayload,
} from '@/features/analysis/types';
import { RunSectionDetail } from '@/features/analysis/components/run-section-detail';
import { AdvocacyGapAnalysisSection } from '@/features/iep-documents/components/advocacy-gap-analysis';
import { sectionTypeLabel } from '@/lib/section-type-label';
import { GeneratedLanguageNotice } from '@/lib/i18n/generated-language-notice';
import { EtrAnalysisEmptyState } from './etr-analysis-empty-state';
import { EtrAnalysisProcessing } from './etr-analysis-processing';
import { EtrAnalysisOverview } from './etr-analysis-overview';
import { EtrAssessmentCompletenessView } from './etr-assessment-completeness-view';
import { EtrEligibilityReviewView } from './etr-eligibility-review-view';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';

interface EtrAnalysisTabProps {
  childId: number;
  run: AnalysisRunLatest | null;
  source: AnalysisRunSource | null;
  sections: AnalysisRunSection[];
  completeness: EtrCompletenessPayload | null;
  eligibility: EtrEligibilityPayload | null;
  otherSources: AnalysisRunOtherSource[];
  stale: boolean;
  isLoading: boolean;
  /** Set when loading the latest run failed for a reason other than "never
   * analyzed" — the previously loaded `run` (if any) is still shown; offer a
   * retry via `onReload`. */
  loadError?: string | null;
  isTriggering: boolean;
  triggerError: string | null;
  onTrigger: () => void;
  onReload: () => void;
}

function otherSourceLabel(source: AnalysisRunOtherSource): string {
  return source.label ?? `${source.sourceType} #${source.sourceId}`;
}

export function EtrAnalysisTab({
  childId,
  run,
  source,
  sections,
  completeness,
  eligibility,
  otherSources,
  stale,
  isLoading,
  loadError = null,
  isTriggering,
  triggerError,
  onTrigger,
  onReload,
}: EtrAnalysisTabProps) {
  const { t } = useTranslation(['etr-documents', 'iep-documents', 'common']);
  const [activeView, setActiveView] = useState<string>('overview');

  // Ordinary (non-`etr_completeness`/`etr_eligibility`) sections for this
  // ETR, in display order.
  const ordered = useMemo(
    () => [...sections].sort((a, b) => a.displayOrder - b.displayOrder),
    [sections],
  );

  const triggerErrorNotice = triggerError && (
    <Notice
      variant="error"
      title={t('analysisTab.triggerErrorTitle')}
      role="alert"
      data-testid="analysis-trigger-error"
    >
      {triggerError}
    </Notice>
  );

  const loadErrorNotice = loadError && (
    <Notice
      variant="error"
      title={t('analysisTab.loadErrorTitle')}
      role="alert"
      data-testid="analysis-load-error"
    >
      {loadError}
      <div className="mt-3">
        <Button
          variant="secondary"
          size="sm"
          onClick={onReload}
          loading={isLoading}
          data-testid="analysis-load-retry"
        >
          {t('common:ui.tryAgain')}
        </Button>
      </div>
    </Notice>
  );

  // While there's no run yet and a load error is showing, a retry
  // (`isLoading` going back to true) must not swap this notice out for the
  // bare spinner below — that would unmount the "Try again" control the
  // user just activated and drop focus off a live control. The notice
  // (with its button now in a loading state) stays mounted instead.
  if (isLoading && !run && !loadError) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('analysisTab.loadingAnalysis')} />
      </div>
    );
  }

  // Never analyzed: no run has ever included this ETR. A load error here
  // means we don't actually know that — showing the Analyze button could
  // kick off a duplicate run, so offer a retry instead.
  if (!run) {
    return (
      <>
        {triggerErrorNotice}
        {loadErrorNotice || <EtrAnalysisEmptyState onTrigger={onTrigger} isTriggering={isTriggering} />}
      </>
    );
  }

  const runInFlight = run.status === 'Pending' || run.status === 'Running';
  const sourceInFlight = source?.status === 'Pending' || source?.status === 'Running';
  if (runInFlight || sourceInFlight) {
    return (
      <>
        {triggerErrorNotice}
        {loadErrorNotice}
        <EtrAnalysisProcessing onReload={onReload} />
      </>
    );
  }

  if (run.status === 'Error') {
    return (
      <>
        {triggerErrorNotice}
        {loadErrorNotice}
        <div className="flex flex-col items-center justify-center py-16 px-4">
          <Card className="max-w-md text-center">
            <Notice variant="error" title={t('analysisTab.analysisFailedTitle')}>
              {run.errorMessage || t('analysisTab.analysisErrorGeneric')}
            </Notice>
            <div className="mt-4">
              <Button onClick={onTrigger} loading={isTriggering} data-testid="etr-analyze-button">
                {t('analysisTab.retryAnalysis')}
              </Button>
            </div>
          </Card>
        </div>
      </>
    );
  }

  // This ETR's own source failed within an otherwise-completed run.
  if (source?.status === 'Error') {
    return (
      <>
        {triggerErrorNotice}
        {loadErrorNotice}
        <div className="flex flex-col items-center justify-center py-16 px-4">
          <Card className="max-w-md text-center">
            <Notice variant="warning" title={t('analysisTab.sourceFailedTitle')}>
              {source.errorMessage || t('analysisTab.sourceErrorGeneric')}
            </Notice>
            <div className="mt-4">
              <Button onClick={onTrigger} loading={isTriggering} data-testid="etr-analyze-button">
                {t('analysisTab.analyzeThisEtr')}
              </Button>
            </div>
          </Card>
        </div>
      </>
    );
  }

  // Completed, with this ETR's own analysis available.
  const isMultiSource = otherSources.length > 0;
  const hasGapAnalysis = run.advocacyGapAnalysis != null;
  const sectionKinds = ordered.map((s) => s.sectionKind);

  const overviewSummary = isMultiSource
    ? ordered
        .map((s) => s.analysis?.plainLanguageSummary)
        .filter((summary): summary is string => Boolean(summary))
        .join('\n\n')
    : run.overallSummary ?? '';
  const overviewRedFlags: RedFlag[] = isMultiSource
    ? ordered.flatMap((s) => s.analysis?.redFlags ?? [])
    : run.overallRedFlags;

  const sidebarButton = (key: string, label: string, count?: number) => (
    <button
      key={key}
      onClick={() => setActiveView(key)}
      data-testid={`etr-analysis-nav-${key}`}
      className={`w-full text-left px-3 py-2 rounded-button text-[13px] font-medium transition-colors ${
        activeView === key
          ? 'bg-brand-teal-50 text-brand-teal-600 border-l-2 border-l-brand-teal-500'
          : 'text-brand-slate-600 hover:bg-brand-slate-50'
      }`}
    >
      {label}
      {count !== undefined && (
        <span className="ml-2 text-[11px] opacity-70">({count})</span>
      )}
    </button>
  );

  // Built here, outside any JSX expression container, so the literal nav
  // keys ('overview', 'gap-analysis', …) and `sectionTypeLabel`'s 'short'
  // style argument don't trip `i18next/no-literal-string` — see
  // iep-documents/analysis-tab.tsx's identical comment.
  const sidebarButtons = [
    sidebarButton('overview', t('analysisTab.overviewNav')),
    hasGapAnalysis
      ? sidebarButton(
          'gap-analysis',
          t('analysisTab.yourGoalsNav'),
          run.advocacyGapAnalysis?.goalAlignments.length ?? 0,
        )
      : null,
    completeness ? sidebarButton('completeness', t('analysisTab.completenessNav')) : null,
    eligibility ? sidebarButton('eligibility', t('analysisTab.eligibilityNav')) : null,
  ];
  const sectionSidebarButtons = sectionKinds.map((kind) =>
    sidebarButton(kind, sectionTypeLabel(kind, 'short')),
  );

  const renderContent = () => {
    if (activeView === 'overview') {
      return (
        <EtrAnalysisOverview
          overallSummary={overviewSummary}
          redFlags={overviewRedFlags}
          completeness={completeness}
          eligibility={eligibility}
        />
      );
    }

    if (activeView === 'gap-analysis' && run.advocacyGapAnalysis) {
      return <AdvocacyGapAnalysisSection gapAnalysis={run.advocacyGapAnalysis} />;
    }

    if (activeView === 'completeness' && completeness) {
      return <EtrAssessmentCompletenessView data={completeness} />;
    }

    if (activeView === 'eligibility' && eligibility) {
      return <EtrEligibilityReviewView data={eligibility} />;
    }

    const matchedSection = ordered.find((s) => s.sectionKind === activeView);
    if (matchedSection && matchedSection.analysis) {
      return <RunSectionDetail section={matchedSection.analysis} />;
    }

    return null;
  };

  return (
    <div className="space-y-4" data-testid="etr-analysis-tab">
      {triggerErrorNotice}
      {loadErrorNotice}

      <GeneratedLanguageNotice generatedLanguage={run.generatedLanguage} />

      {isMultiSource && (
        <Notice
          variant="info"
          title={t('analysisTab.multiSourceInfo', { sources: otherSources.map(otherSourceLabel).join(', ') })}
          data-testid="analysis-multi-source-info"
        >
          <Link
            to={`/children/${childId}/analysis?run=${run.id}`}
            className="font-medium text-brand-teal-600 underline underline-offset-2 hover:text-brand-teal-700 rounded-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-teal-500"
          >
            {t('analysisTab.viewFullAnalysis')}
          </Link>
        </Notice>
      )}

      {stale && (
        <div className="flex flex-wrap items-center justify-between gap-4" data-testid="analysis-stale-banner">
          <div className="min-w-[16rem] flex-1">
            <Notice variant="warning" title={t('analysisTab.staleTitle')}>
              {t('analysisTab.staleBody')}
            </Notice>
          </div>
          <Button
            variant="amber"
            onClick={onTrigger}
            loading={isTriggering}
            data-testid="reanalyze-button"
            className="shrink-0"
          >
            {t('analysisTab.reanalyze')}
          </Button>
        </div>
      )}

      <div className="flex gap-4 min-h-[500px]">
        <nav className="w-56 shrink-0 space-y-0.5">
          {sidebarButtons}

          <div className="border-t border-brand-slate-200 my-2" />

          {sectionSidebarButtons}
        </nav>

        <Card className="flex-1 overflow-y-auto">{renderContent()}</Card>
      </div>
    </div>
  );
}
