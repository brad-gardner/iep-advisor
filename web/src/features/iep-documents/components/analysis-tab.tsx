import { useMemo, useState } from 'react';
import type { AdvocacyGoal, IepAnalysis } from '@/types/api';
import { AnalysisEmptyState } from './analysis-empty-state';
import { AnalysisProcessing } from './analysis-processing';
import { AnalysisOverview } from './analysis-overview';
import { AnalysisSectionDetail } from './analysis-section-detail';
import { AnalysisGoalsList } from './analysis-goals-list';
import { AdvocacyGapAnalysisSection } from './advocacy-gap-analysis';
import { StaleAnalysisBanner } from './stale-analysis-banner';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

interface AnalysisTabProps {
  analysis: IepAnalysis | null;
  isLoading: boolean;
  isTriggering: boolean;
  advocacyGoals: AdvocacyGoal[];
  onTrigger: () => void;
  onReload: () => void;
}

const SECTION_LABELS: Record<string, string> = {
  student_profile: 'Student Profile',
  present_levels: 'Present Levels',
  evaluations: 'Evaluations',
  assessments: 'Assessments',
  eligibility: 'Eligibility',
  annual_goals: 'Annual Goals',
  services: 'Services',
  accommodations: 'Accommodations',
  placement: 'Placement',
  transition: 'Transition',
  progress_monitoring: 'Progress Monitoring',
  other: 'Other',
};

export function AnalysisTab({
  analysis,
  isLoading,
  isTriggering,
  advocacyGoals,
  onTrigger,
  onReload,
}: AnalysisTabProps) {
  const [activeView, setActiveView] = useState<string>('overview');

  // Staleness detection: goals changed after analysis was created
  const isStale = useMemo(() => {
    if (!analysis || analysis.status !== 'completed') return false;
    if (advocacyGoals.length === 0 && (!analysis.parentGoalsSnapshot || analysis.parentGoalsSnapshot.length === 0)) return false;

    const analysisTime = new Date(analysis.createdAt).getTime();

    const goalsChangedAfter = advocacyGoals.some((g) => {
      const created = new Date(g.createdAt).getTime();
      const updated = new Date(g.updatedAt).getTime();
      return created > analysisTime || updated > analysisTime;
    });

    const snapshotCount = analysis.parentGoalsSnapshot?.length ?? 0;
    const countDiffers = advocacyGoals.length !== snapshotCount;

    return goalsChangedAfter || countDiffers;
  }, [analysis, advocacyGoals]);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!analysis || analysis.status === 'pending' || analysis.status === 'analyzing') {
    if (analysis?.status === 'analyzing' || analysis?.status === 'pending') {
      return <AnalysisProcessing onReload={onReload} />;
    }
    return <AnalysisEmptyState onTrigger={onTrigger} isTriggering={isTriggering} />;
  }

  if (analysis.status === 'error') {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <Card className="max-w-md text-center">
          <Notice variant="error" title="Analysis Failed">
            {analysis.errorMessage || 'An error occurred during analysis.'}
          </Notice>
          <div className="mt-4">
            <Button onClick={onTrigger} disabled={isTriggering}>
              {isTriggering ? 'Retrying...' : 'Retry Analysis'}
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  const sectionTypes = analysis.sectionAnalyses.map((sa) => sa.sectionType);
  const hasGoals = analysis.goalAnalyses.length > 0;
  const hasGapAnalysis = analysis.advocacyGapAnalysis != null;

  const sidebarButton = (key: string, label: string, count?: number) => (
    <button
      key={key}
      onClick={() => setActiveView(key)}
      data-testid={`analysis-nav-${key}`}
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

  const renderContent = () => {
    if (activeView === 'overview') {
      return (
        <>
          {isStale && (
            <div className="mb-6">
              <StaleAnalysisBanner onReanalyze={onTrigger} isReanalyzing={isTriggering} />
            </div>
          )}
          <AnalysisOverview
            overallSummary={analysis.overallSummary || ''}
            overallRedFlags={analysis.overallRedFlags}
            suggestedQuestions={analysis.suggestedQuestions}
          />
        </>
      );
    }

    if (activeView === 'gap-analysis' && analysis.advocacyGapAnalysis) {
      return <AdvocacyGapAnalysisSection gapAnalysis={analysis.advocacyGapAnalysis} />;
    }

    if (activeView === 'goals') {
      return <AnalysisGoalsList goalAnalyses={analysis.goalAnalyses} />;
    }

    const sectionAnalysis = analysis.sectionAnalyses.find(
      (sa) => sa.sectionType === activeView
    );
    if (sectionAnalysis) {
      return <AnalysisSectionDetail sectionAnalysis={sectionAnalysis} />;
    }

    return null;
  };

  return (
    <div className="flex gap-4 min-h-[500px]">
      <nav className="w-56 shrink-0 space-y-0.5">
        {sidebarButton('overview', 'Overview')}

        {hasGapAnalysis &&
          sidebarButton(
            'gap-analysis',
            'Your Goals',
            analysis.advocacyGapAnalysis?.goalAlignments.length ?? 0
          )}

        {hasGoals &&
          sidebarButton('goals', 'Goal Analysis', analysis.goalAnalyses.length)}

        <div className="border-t border-brand-slate-200 my-2" />

        {sectionTypes.map((type) =>
          sidebarButton(type, SECTION_LABELS[type] || type)
        )}
      </nav>

      <Card className="flex-1 overflow-y-auto">
        {renderContent()}
      </Card>
    </div>
  );
}
