import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { useEtrAnalysis } from '../hooks/use-etr-analysis';
import { parseEtrAnalysis } from '../lib/parse-analysis';
import { EtrAnalysisEmptyState } from './etr-analysis-empty-state';
import { EtrAnalysisProcessing } from './etr-analysis-processing';
import { EtrAnalysisOverview } from './etr-analysis-overview';
import { EtrAssessmentCompletenessView } from './etr-assessment-completeness-view';
import { EtrEligibilityReviewView } from './etr-eligibility-review-view';
import { EtrRedFlagsList } from './etr-red-flags-list';
import { EtrSuggestedQuestionsList } from './etr-suggested-questions-list';

interface EtrAnalysisTabProps {
  etrId: number;
}

type AnalysisView =
  | 'overview'
  | 'assessment'
  | 'eligibility'
  | 'red-flags'
  | 'questions';

const VIEW_LABELS: Record<AnalysisView, string> = {
  overview: 'Overview',
  assessment: 'Assessment Completeness',
  eligibility: 'Eligibility Review',
  'red-flags': 'Red Flags',
  questions: 'Suggested Questions',
};

export function EtrAnalysisTab({ etrId }: EtrAnalysisTabProps) {
  const { analysis, status, loading, isTriggering, error, start, refresh } =
    useEtrAnalysis(etrId);
  const [activeView, setActiveView] = useState<AnalysisView>('overview');

  const parsed = useMemo(
    () => (analysis ? parseEtrAnalysis(analysis) : null),
    [analysis]
  );

  if (loading && !analysis) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (error && !analysis) {
    return (
      <div className="py-4">
        <Notice variant="error" title="Couldn't load analysis">
          {error}
        </Notice>
      </div>
    );
  }

  if (status === 'none') {
    return <EtrAnalysisEmptyState onStart={start} isTriggering={isTriggering} />;
  }

  if (status === 'pending' || status === 'analyzing') {
    return <EtrAnalysisProcessing onReload={refresh} />;
  }

  if (status === 'error') {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <Card className="max-w-md text-center">
          <Notice variant="error" title="Analysis Failed">
            {analysis?.errorMessage || 'An error occurred during analysis.'}
          </Notice>
          <div className="mt-4">
            <Button
              onClick={start}
              disabled={isTriggering}
              data-testid="etr-retry-analysis-button"
            >
              {isTriggering ? 'Retrying...' : 'Retry Analysis'}
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  // status === 'completed'
  if (!parsed) {
    return (
      <div className="py-4">
        <Notice variant="error" title="Analysis data is malformed">
          The analysis completed but the results could not be parsed.
        </Notice>
      </div>
    );
  }

  const tabs: AnalysisView[] = [
    'overview',
    'assessment',
    'eligibility',
    'red-flags',
    'questions',
  ];

  const renderContent = () => {
    switch (activeView) {
      case 'overview':
        return <EtrAnalysisOverview parsed={parsed} />;
      case 'assessment':
        return parsed.assessmentCompleteness ? (
          <EtrAssessmentCompletenessView data={parsed.assessmentCompleteness} />
        ) : (
          <EmptySection message="No assessment completeness data available." />
        );
      case 'eligibility':
        return parsed.eligibilityReview ? (
          <EtrEligibilityReviewView data={parsed.eligibilityReview} />
        ) : (
          <EmptySection message="No eligibility review data available." />
        );
      case 'red-flags':
        return <EtrRedFlagsList redFlags={parsed.redFlags} />;
      case 'questions':
        return <EtrSuggestedQuestionsList questions={parsed.suggestedQuestions} />;
    }
  };

  return (
    <div className="space-y-4" data-testid="etr-analysis-tab">
      <nav className="flex gap-1 flex-wrap border-b border-brand-slate-200">
        {tabs.map((view) => {
          const isActive = activeView === view;
          const count =
            view === 'red-flags'
              ? parsed.redFlags.length
              : view === 'questions'
                ? parsed.suggestedQuestions.length
                : undefined;
          return (
            <button
              key={view}
              type="button"
              onClick={() => setActiveView(view)}
              data-testid={`etr-analysis-nav-${view}`}
              aria-selected={isActive}
              className={`px-3 py-2 text-[13px] font-medium transition-colors ${
                isActive
                  ? 'text-brand-slate-800 border-b-2 border-brand-teal-500'
                  : 'text-brand-slate-400 hover:text-brand-slate-800'
              }`}
            >
              {VIEW_LABELS[view]}
              {count !== undefined && (
                <span className="ml-1.5 text-[11px] opacity-70">({count})</span>
              )}
            </button>
          );
        })}
      </nav>

      <Card>{renderContent()}</Card>
    </div>
  );
}

function EmptySection({ message }: { message: string }) {
  return (
    <div className="text-sm text-brand-slate-400 py-8 text-center">{message}</div>
  );
}
