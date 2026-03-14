import type { RedFlag, SuggestedQuestion } from '@/types/api';
import { RedFlagCard } from './red-flag-card';

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
        <h2 className="text-xl font-semibold mb-3 text-gray-900">Overview</h2>
        <div className="text-gray-600 leading-relaxed whitespace-pre-wrap">
          {overallSummary}
        </div>
      </section>

      {overallRedFlags.length > 0 && (
        <section>
          <h2 className="text-xl font-semibold mb-3 text-gray-900">
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
          <h2 className="text-xl font-semibold mb-3 text-gray-900">
            Questions for Your IEP Meeting
          </h2>
          <div className="space-y-4">
            {Object.entries(questionsByCategory).map(([category, questions]) => (
              <div key={category}>
                <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wide mb-2">
                  {CATEGORY_LABELS[category] || category}
                </h3>
                <div className="space-y-2">
                  {questions.map((q, i) => (
                    <div key={i} className="bg-gray-50 rounded p-3 border border-gray-200">
                      <p className="font-medium text-sm text-gray-900">{q.question}</p>
                      <p className="text-xs text-gray-500 mt-1">{q.context}</p>
                    </div>
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
