import type { GoalAnalysis } from '@/types/api';
import { AnalysisGoalCard } from './analysis-goal-card';

interface AnalysisGoalsListProps {
  goalAnalyses: GoalAnalysis[];
}

export function AnalysisGoalsList({ goalAnalyses }: AnalysisGoalsListProps) {
  if (goalAnalyses.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-brand-slate-400">No goal analyses available.</p>
      </div>
    );
  }

  const greenCount = goalAnalyses.filter((g) => g.overallRating === 'green').length;
  const yellowCount = goalAnalyses.filter((g) => g.overallRating === 'yellow').length;
  const redCount = goalAnalyses.filter((g) => g.overallRating === 'red').length;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-serif text-[22px] font-semibold mb-2 text-brand-slate-800">
          Goal Analysis ({goalAnalyses.length} goals)
        </h2>
        <div className="flex gap-4 text-[13px] font-medium">
          {greenCount > 0 && (
            <span className="text-brand-teal-600">{greenCount} strong</span>
          )}
          {yellowCount > 0 && (
            <span className="text-brand-amber-500">{yellowCount} need improvement</span>
          )}
          {redCount > 0 && (
            <span className="text-brand-red">{redCount} significant concerns</span>
          )}
        </div>
      </div>

      <div className="space-y-4">
        {goalAnalyses.map((ga) => (
          <AnalysisGoalCard key={ga.goalId} goalAnalysis={ga} />
        ))}
      </div>
    </div>
  );
}
