import type { GoalAnalysis } from '@/types/api';
import { AnalysisGoalCard } from './analysis-goal-card';

interface AnalysisGoalsListProps {
  goalAnalyses: GoalAnalysis[];
}

export function AnalysisGoalsList({ goalAnalyses }: AnalysisGoalsListProps) {
  if (goalAnalyses.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-gray-500">No goal analyses available.</p>
      </div>
    );
  }

  const greenCount = goalAnalyses.filter((g) => g.overallRating === 'green').length;
  const yellowCount = goalAnalyses.filter((g) => g.overallRating === 'yellow').length;
  const redCount = goalAnalyses.filter((g) => g.overallRating === 'red').length;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold mb-2 text-gray-900">
          Goal Analysis ({goalAnalyses.length} goals)
        </h2>
        <div className="flex gap-4 text-sm">
          {greenCount > 0 && (
            <span className="text-green-600">{greenCount} strong</span>
          )}
          {yellowCount > 0 && (
            <span className="text-yellow-600">{yellowCount} need improvement</span>
          )}
          {redCount > 0 && (
            <span className="text-red-600">{redCount} significant concerns</span>
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
