import { useMemo, useState } from 'react';
import type { AdvocacyGoal, IepAnalysis } from '@/types/api';
import { AnalysisEmptyState } from './analysis-empty-state';
import { AnalysisProcessing } from './analysis-processing';
import { AnalysisOverview } from './analysis-overview';
import { AnalysisSectionDetail } from './analysis-section-detail';
import { AnalysisGoalsList } from './analysis-goals-list';
import { AdvocacyGapAnalysisSection } from './advocacy-gap-analysis';
import { StaleAnalysisBanner } from './stale-analysis-banner';

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
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900" />
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
        <div className="bg-red-50 border border-red-300 rounded-lg p-6 max-w-md text-center">
          <h3 className="text-lg font-semibold text-red-700 mb-2">Analysis Failed</h3>
          <p className="text-sm text-gray-600 mb-4">
            {analysis.errorMessage || 'An error occurred during analysis.'}
          </p>
          <button
            onClick={onTrigger}
            disabled={isTriggering}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded text-sm text-white transition-colors"
          >
            {isTriggering ? 'Retrying...' : 'Retry Analysis'}
          </button>
        </div>
      </div>
    );
  }

  const sectionTypes = analysis.sectionAnalyses.map((sa) => sa.sectionType);
  const hasGoals = analysis.goalAnalyses.length > 0;
  const hasGapAnalysis = analysis.advocacyGapAnalysis != null;

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
      <nav className="w-56 shrink-0 space-y-1">
        <button
          onClick={() => setActiveView('overview')}
          className={`w-full text-left px-3 py-2 rounded text-sm transition-colors ${
            activeView === 'overview'
              ? 'bg-blue-600 text-white'
              : 'text-gray-600 hover:bg-gray-100'
          }`}
        >
          Overview
        </button>

        {hasGapAnalysis && (
          <button
            onClick={() => setActiveView('gap-analysis')}
            className={`w-full text-left px-3 py-2 rounded text-sm transition-colors ${
              activeView === 'gap-analysis'
                ? 'bg-blue-600 text-white'
                : 'text-gray-600 hover:bg-gray-100'
            }`}
          >
            Your Goals
            <span className="ml-2 text-xs opacity-70">
              ({analysis.advocacyGapAnalysis?.goalAlignments.length ?? 0})
            </span>
          </button>
        )}

        {hasGoals && (
          <button
            onClick={() => setActiveView('goals')}
            className={`w-full text-left px-3 py-2 rounded text-sm transition-colors ${
              activeView === 'goals'
                ? 'bg-blue-600 text-white'
                : 'text-gray-600 hover:bg-gray-100'
            }`}
          >
            Goal Analysis
            <span className="ml-2 text-xs opacity-70">
              ({analysis.goalAnalyses.length})
            </span>
          </button>
        )}

        <div className="border-t border-gray-200 my-2" />

        {sectionTypes.map((type) => (
          <button
            key={type}
            onClick={() => setActiveView(type)}
            className={`w-full text-left px-3 py-2 rounded text-sm transition-colors ${
              activeView === type
                ? 'bg-blue-600 text-white'
                : 'text-gray-600 hover:bg-gray-100'
            }`}
          >
            {SECTION_LABELS[type] || type}
          </button>
        ))}
      </nav>

      <div className="flex-1 bg-white rounded-lg p-6 overflow-y-auto shadow-sm border border-gray-200">
        {renderContent()}
      </div>
    </div>
  );
}
