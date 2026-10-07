import { useTranslation } from 'react-i18next';
import type { AdvocacyGapAnalysis } from '@/types/api';
import { GoalAlignmentCard } from './goal-alignment-card';

interface AdvocacyGapAnalysisProps {
  gapAnalysis: AdvocacyGapAnalysis;
}

export function AdvocacyGapAnalysisSection({ gapAnalysis }: AdvocacyGapAnalysisProps) {
  const { t } = useTranslation('iep-documents');
  const addressed = gapAnalysis.goalAlignments.filter(
    (g) => g.alignmentStatus === 'addressed'
  ).length;
  const total = gapAnalysis.goalAlignments.length;

  return (
    <section className="space-y-4">
      <div>
        <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-1">
          {t('gapAnalysis.heading', { addressed, total })}
        </h2>
        <p className="text-sm text-brand-slate-600">{gapAnalysis.summary}</p>
      </div>

      <div className="space-y-3">
        {gapAnalysis.goalAlignments.map((alignment, i) => (
          <GoalAlignmentCard key={i} alignment={alignment} />
        ))}
      </div>
    </section>
  );
}
