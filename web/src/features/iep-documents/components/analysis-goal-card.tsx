import type { GoalAnalysis } from '@/types/api';
import { SmartCriteriaGrid } from './smart-criteria-grid';

interface AnalysisGoalCardProps {
  goalAnalysis: GoalAnalysis;
}

const RATING_BADGES: Record<string, { label: string; className: string }> = {
  green: { label: 'Strong', className: 'bg-green-100 text-green-700 border-green-300' },
  yellow: { label: 'Needs Improvement', className: 'bg-yellow-100 text-yellow-700 border-yellow-300' },
  red: { label: 'Significant Concerns', className: 'bg-red-100 text-red-700 border-red-300' },
};

export function AnalysisGoalCard({ goalAnalysis }: AnalysisGoalCardProps) {
  const badge = RATING_BADGES[goalAnalysis.overallRating] || RATING_BADGES.yellow;

  return (
    <div className="bg-gray-50 rounded-lg p-5 space-y-4 border border-gray-200">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1">
          {goalAnalysis.domain && (
            <span className="text-xs text-gray-500 uppercase tracking-wide">
              {goalAnalysis.domain}
            </span>
          )}
          <p className="font-medium text-sm mt-1 text-gray-900">{goalAnalysis.goalText}</p>
        </div>
        <span className={`shrink-0 text-xs px-2 py-1 rounded border ${badge.className}`}>
          {badge.label}
        </span>
      </div>

      <p className="text-sm text-gray-600">{goalAnalysis.plainLanguageSummary}</p>

      <SmartCriteriaGrid smartAnalysis={goalAnalysis.smartAnalysis} />

      {goalAnalysis.strengths.length > 0 && (
        <div>
          <h4 className="text-sm font-medium text-green-600 mb-1">Strengths</h4>
          <ul className="space-y-1">
            {goalAnalysis.strengths.map((s, i) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-green-600 shrink-0">+</span>
                <span>{s}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {goalAnalysis.concerns.length > 0 && (
        <div>
          <h4 className="text-sm font-medium text-yellow-600 mb-1">Concerns</h4>
          <ul className="space-y-1">
            {goalAnalysis.concerns.map((c, i) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-yellow-600 shrink-0">-</span>
                <span>{c}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {goalAnalysis.suggestedImprovements.length > 0 && (
        <div>
          <h4 className="text-sm font-medium text-blue-600 mb-1">Suggested Improvements</h4>
          <ul className="space-y-1">
            {goalAnalysis.suggestedImprovements.map((imp, i) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-blue-600 shrink-0">*</span>
                <span>{imp}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
