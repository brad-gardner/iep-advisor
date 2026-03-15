import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowRight } from 'lucide-react';
import { useIepComparison } from '../hooks/use-iep-comparison';
import { ComparisonSummary } from './comparison-summary';
import { GoalDiffCard } from './goal-diff-card';
import { SectionDiff } from './section-diff';
import { RedFlagResolution } from './red-flag-resolution';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

function formatDate(dateStr: string) {
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

export function ComparisonView({
  iepId,
  otherId,
  childId,
}: {
  iepId: number;
  otherId: number;
  childId: number;
}) {
  const { comparison, isLoading, error } = useIepComparison(iepId, otherId);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (error || !comparison) {
    return (
      <div className="space-y-4">
        <Link
          to={`/children/${childId}`}
          className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" strokeWidth={1.8} />
          Back to child
        </Link>
        <Notice variant="error" title="Comparison failed">
          {error || 'Unable to load the comparison. Please try again.'}
        </Notice>
      </div>
    );
  }

  const { goalChanges, sectionChanges, redFlagResolution, summary } = comparison;
  const hasGoalChanges =
    goalChanges.added.length > 0 ||
    goalChanges.removed.length > 0 ||
    goalChanges.modified.length > 0;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <Link
          to={`/children/${childId}`}
          className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" strokeWidth={1.8} />
          Back to child
        </Link>

        <h1 className="font-serif text-[32px] font-semibold leading-tight mt-1 text-brand-slate-800">
          IEP Comparison
        </h1>

        <div className="flex items-center gap-2 mt-2 text-[15px] text-brand-slate-600">
          <span className="font-medium">{formatDate(comparison.olderDate)}</span>
          <ArrowRight className="w-4 h-4 text-brand-slate-400" strokeWidth={1.8} />
          <span className="font-medium">{formatDate(comparison.newerDate)}</span>
        </div>
      </div>

      {/* Summary stats */}
      <ComparisonSummary summary={summary} />

      {/* Goal changes */}
      {hasGoalChanges ? (
        <Card>
          <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800 mb-4">
            Goal Changes
          </h3>
          <div className="space-y-3">
            {goalChanges.added.map((goal, i) => (
              <GoalDiffCard key={`added-${i}`} type="added" goal={goal} />
            ))}
            {goalChanges.removed.map((goal, i) => (
              <GoalDiffCard key={`removed-${i}`} type="removed" goal={goal} />
            ))}
            {goalChanges.modified.map((goal, i) => (
              <GoalDiffCard key={`modified-${i}`} type="modified" goal={goal} />
            ))}
          </div>
        </Card>
      ) : (
        <Card>
          <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800 mb-2">
            Goal Changes
          </h3>
          <p className="text-[13px] text-brand-slate-400">No goal changes detected between these IEPs.</p>
        </Card>
      )}

      {/* Section changes */}
      <SectionDiff changes={sectionChanges} />

      {/* Red flag resolution */}
      <RedFlagResolution resolution={redFlagResolution} />
    </div>
  );
}
