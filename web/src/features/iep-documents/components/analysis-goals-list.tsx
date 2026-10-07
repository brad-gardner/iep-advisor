import { useTranslation } from 'react-i18next';
import type { GoalAnalysis } from '@/types/api';
import { AnalysisGoalCard } from './analysis-goal-card';

interface AnalysisGoalsListProps {
  goalAnalyses: GoalAnalysis[];
  childId?: number;
  canAsk?: boolean;
  /** Heading level for "Goal Analysis (N goals)" — 2 (default) when this is
   * the top-level heading for its view, 3 when nested under another h2
   * (e.g. a source's own label on the child-level run detail page). */
  headingLevel?: 2 | 3;
}

export function AnalysisGoalsList({
  goalAnalyses,
  childId,
  canAsk,
  headingLevel = 2,
}: AnalysisGoalsListProps) {
  const { t } = useTranslation('iep-documents');
  if (goalAnalyses.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-brand-slate-500">{t('goalsList.empty')}</p>
      </div>
    );
  }

  const greenCount = goalAnalyses.filter((g) => g.overallRating === 'green').length;
  const yellowCount = goalAnalyses.filter((g) => g.overallRating === 'yellow').length;
  const redCount = goalAnalyses.filter((g) => g.overallRating === 'red').length;
  const Heading = headingLevel === 3 ? 'h3' : 'h2';

  return (
    <div className="space-y-6">
      <div>
        <Heading className="font-serif text-[22px] font-semibold mb-2 text-brand-slate-800">
          {t('goalsList.heading', { count: goalAnalyses.length })}
        </Heading>
        <div className="flex gap-4 text-[13px] font-medium">
          {greenCount > 0 && (
            <span className="text-brand-teal-600">{t('goalsList.strongCount', { count: greenCount })}</span>
          )}
          {yellowCount > 0 && (
            <span className="text-brand-amber-500">{t('goalsList.needsImprovementCount', { count: yellowCount })}</span>
          )}
          {redCount > 0 && (
            <span className="text-brand-danger-700">{t('goalsList.significantConcernsCount', { count: redCount })}</span>
          )}
        </div>
      </div>

      <div className="space-y-4">
        {goalAnalyses.map((ga) => (
          <AnalysisGoalCard key={ga.goalId} goalAnalysis={ga} childId={childId} canAsk={canAsk} />
        ))}
      </div>
    </div>
  );
}
