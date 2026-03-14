import { MessageCircleQuestion } from 'lucide-react';
import type { RedFlag, SuggestedQuestion } from '@/types/api';
import { RedFlagCard } from './red-flag-card';
import { Notice } from '@/components/ui/notice';

interface AnalysisOverviewProps {
  overallSummary: string;
  overallRedFlags: RedFlag[];
  suggestedQuestions: SuggestedQuestion[];
}

const CATEGORY_LABELS: Record<string, string> = {
  goals: 'Goals',
  services: 'Services',
  placement: 'Placement',
  rights: 'Your Rights',
  general: 'General',
};

export function AnalysisOverview({
  overallSummary,
  overallRedFlags,
  suggestedQuestions,
}: AnalysisOverviewProps) {
  const questionsByCategory = suggestedQuestions.reduce<Record<string, SuggestedQuestion[]>>(
    (acc, q) => {
      const cat = q.category || 'general';
      if (!acc[cat]) acc[cat] = [];
      acc[cat].push(q);
      return acc;
    },
    {}
  );

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

      {suggestedQuestions.length > 0 && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
            Questions for Your IEP Meeting
          </h2>
          <div className="space-y-4">
            {Object.entries(questionsByCategory).map(([category, questions]) => (
              <div key={category}>
                <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
                  {CATEGORY_LABELS[category] || category}
                </h3>
                <div className="space-y-2">
                  {questions.map((q, i) => (
                    <Notice key={i} variant="info" title={q.question}>
                      <div className="flex items-start gap-1.5">
                        <MessageCircleQuestion className="w-3.5 h-3.5 text-brand-teal-500 shrink-0 mt-0.5" strokeWidth={1.8} aria-hidden="true" />
                        <span className="text-[11px] text-brand-slate-400">{q.context}</span>
                      </div>
                    </Notice>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
