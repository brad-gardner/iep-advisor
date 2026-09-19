import { CircleCheck, AlertTriangle, Lightbulb } from 'lucide-react';
import type { GoalAnalysis } from '@/types/api';
import { SmartCriteriaGrid } from './smart-criteria-grid';
import { Badge } from '@/components/ui/badge';
import { AskAdvocateButton } from '@/features/advocate/components/ask-advocate-button';
import { goalAnchorId } from '@/features/advocate/lib/citation-links';

interface AnalysisGoalCardProps {
  goalAnalysis: GoalAnalysis;
  /** When set (with `canAsk`), each card carries an "Ask the advocate" launcher about that goal. */
  childId?: number;
  canAsk?: boolean;
}

const RATING_BADGES: Record<string, { label: string; variant: 'success' | 'warning' | 'error' }> = {
  green: { label: 'Strong', variant: 'success' },
  yellow: { label: 'Needs Improvement', variant: 'warning' },
  red: { label: 'Significant Concerns', variant: 'error' },
};

export function AnalysisGoalCard({ goalAnalysis, childId, canAsk = false }: AnalysisGoalCardProps) {
  const badge = RATING_BADGES[goalAnalysis.overallRating] || RATING_BADGES.yellow;
  const goalName = goalAnalysis.domain ? `${goalAnalysis.domain} goal` : 'goal';

  return (
    <div
      id={goalAnchorId(goalAnalysis.goalId)}
      className="bg-white rounded-card border-[0.5px] border-brand-slate-200 p-5 space-y-4 scroll-mt-20"
      data-testid={`analysis-goal-${goalAnalysis.goalId}`}
    >
      {/* Eyebrow + badge */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1">
          {goalAnalysis.domain && (
            <span className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide">
              GOAL &middot; {goalAnalysis.domain.toUpperCase()}
            </span>
          )}
          <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800 mt-1">
            {goalAnalysis.goalText}
          </h3>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Badge variant={badge.variant} className="shrink-0">
            {badge.label}
          </Badge>
          {childId != null && canAsk && (
            <AskAdvocateButton
              childId={childId}
              about={{ kind: 'goal', id: goalAnalysis.goalId }}
              label={`the ${goalName}`}
              appearance="icon"
              ariaLabel={`Ask the advocate about this ${goalName}`}
            />
          )}
        </div>
      </div>

      {/* Goal text in italic panel */}
      <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
        <p className="text-sm italic text-brand-slate-600 leading-relaxed">
          {goalAnalysis.plainLanguageSummary}
        </p>
      </div>

      <SmartCriteriaGrid smartAnalysis={goalAnalysis.smartAnalysis} />

      {/* What this means panel — teal left border */}
      {goalAnalysis.strengths.length > 0 && (
        <div className="border-l-2 border-l-brand-teal-500 bg-brand-teal-50 rounded-r-card p-3">
          <h4 className="text-[13px] font-medium text-brand-teal-600 mb-1.5 flex items-center gap-1.5">
            <CircleCheck className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            Strengths
          </h4>
          <ul className="space-y-1">
            {goalAnalysis.strengths.map((s, i) => (
              <li key={i} className="text-sm text-brand-slate-600 flex gap-2">
                <span className="text-brand-teal-500 shrink-0">+</span>
                <span>{s}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Flag panel — amber left border */}
      {goalAnalysis.concerns.length > 0 && (
        <div className="border-l-2 border-l-brand-amber-400 bg-brand-amber-50 rounded-r-card p-3">
          <h4 className="text-[13px] font-medium text-brand-amber-500 mb-1.5 flex items-center gap-1.5">
            <AlertTriangle className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            Concerns
          </h4>
          <ul className="space-y-1">
            {goalAnalysis.concerns.map((c, i) => (
              <li key={i} className="text-sm text-brand-slate-600 flex gap-2">
                <span className="text-brand-amber-500 shrink-0">-</span>
                <span>{c}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {goalAnalysis.suggestedImprovements.length > 0 && (
        <div className="border-l-2 border-l-brand-teal-500 bg-brand-teal-50 rounded-r-card p-3">
          <h4 className="text-[13px] font-medium text-brand-teal-600 mb-1.5 flex items-center gap-1.5">
            <Lightbulb className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            Suggested Improvements
          </h4>
          <ul className="space-y-1">
            {goalAnalysis.suggestedImprovements.map((imp, i) => (
              <li key={i} className="text-sm text-brand-slate-600 flex gap-2">
                <span className="text-brand-teal-500 shrink-0">*</span>
                <span>{imp}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
