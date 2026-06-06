import type { RedFlag } from '@/types/api';
import { RedFlagCard } from './red-flag-card';

interface AnalysisOverviewProps {
  overallSummary: string;
  overallRedFlags: RedFlag[];
}

export function AnalysisOverview({
  overallSummary,
  overallRedFlags,
}: AnalysisOverviewProps) {
  return (
    <div className="space-y-8">
      <section>
        <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
          Overview
        </h2>
        <div className="text-brand-slate-600 text-sm leading-relaxed whitespace-pre-wrap">
          {overallSummary}
        </div>
      </section>

      {overallRedFlags.length > 0 && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
            Areas of Concern ({overallRedFlags.length})
          </h2>
          <div className="space-y-3">
            {overallRedFlags.map((flag, i) => (
              <RedFlagCard key={i} redFlag={flag} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
