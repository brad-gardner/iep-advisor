import { MessageCircleQuestion } from 'lucide-react';
import type { EtrSuggestedQuestion } from '../types';

interface EtrSuggestedQuestionCardProps {
  question: EtrSuggestedQuestion;
}

export function EtrSuggestedQuestionCard({ question }: EtrSuggestedQuestionCardProps) {
  return (
    <div
      className="bg-brand-teal-50 border border-brand-teal-100 rounded-card p-3"
      data-testid="etr-suggested-question-card"
    >
      <div className="flex items-start gap-2">
        <MessageCircleQuestion
          className="w-4 h-4 text-brand-teal-500 shrink-0 mt-0.5"
          strokeWidth={1.8}
          aria-hidden="true"
        />
        <div className="flex-1">
          <p className="text-sm font-medium text-brand-teal-600">{question.question}</p>
          {question.rationale && (
            <p className="text-[12px] text-brand-slate-500 mt-1">{question.rationale}</p>
          )}
        </div>
      </div>
    </div>
  );
}
