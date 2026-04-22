import type { EtrSuggestedQuestion } from '../types';
import { EtrSuggestedQuestionCard } from './etr-suggested-question-card';

interface EtrSuggestedQuestionsListProps {
  questions: EtrSuggestedQuestion[];
}

const CATEGORY_LABELS: Record<string, string> = {
  clarification: 'Clarification',
  challenge_eligibility: 'Challenge Eligibility',
  iee_request: 'Independent Educational Evaluation (IEE)',
  procedural: 'Procedural',
  services_next_steps: 'Services & Next Steps',
};

const CATEGORY_ORDER = [
  'challenge_eligibility',
  'iee_request',
  'clarification',
  'services_next_steps',
  'procedural',
];

export function EtrSuggestedQuestionsList({ questions }: EtrSuggestedQuestionsListProps) {
  if (questions.length === 0) {
    return (
      <div className="text-sm text-brand-slate-400 py-8 text-center">
        No suggested questions generated.
      </div>
    );
  }

  const byCategory = questions.reduce<Record<string, EtrSuggestedQuestion[]>>((acc, q) => {
    const cat = q.category || 'clarification';
    if (!acc[cat]) acc[cat] = [];
    acc[cat].push(q);
    return acc;
  }, {});

  const knownCategories = CATEGORY_ORDER.filter((c) => byCategory[c]?.length);
  const unknownCategories = Object.keys(byCategory).filter(
    (c) => !CATEGORY_ORDER.includes(c)
  );
  const orderedCategories = [...knownCategories, ...unknownCategories];

  return (
    <div className="space-y-5" data-testid="etr-suggested-questions-list">
      {orderedCategories.map((category) => (
        <div key={category}>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            {CATEGORY_LABELS[category] || category}
          </h3>
          <div className="space-y-2">
            {byCategory[category].map((q, i) => (
              <EtrSuggestedQuestionCard key={i} question={q} />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
