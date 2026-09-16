import { useState } from 'react';
import { Lightbulb } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useDraftReviewContext } from '../hooks/draft-review-context';

interface ExplainPanelProps {
  fieldKey: string;
  rowId: string | null;
  'data-testid': string;
}

/**
 * "Explain" toggle for one goal/service/accommodation row (or field). The
 * underlying revision-wide explanation set is fetched once, lazily, the first
 * time ANY card asks for it (`useDraftExplanations`); this card only shows its
 * own loading/error state once it has asked.
 */
export function ExplainPanel({ fieldKey, rowId, 'data-testid': testId }: ExplainPanelProps) {
  const ctx = useDraftReviewContext();
  const [revealed, setRevealed] = useState(false);
  if (!ctx) return null;
  const { explanations } = ctx;
  const explanation = explanations.getItemExplanation(fieldKey, rowId);

  const handleToggle = () => {
    if (!revealed) explanations.ensureLoaded();
    setRevealed((r) => !r);
  };

  return (
    <div>
      <Button
        size="sm"
        variant="ghost"
        onClick={handleToggle}
        aria-expanded={revealed}
        data-testid={testId}
      >
        <Lightbulb className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
        {revealed ? 'Hide explanation' : 'Explain'}
      </Button>
      {revealed && (
        <div className="mt-2 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-3 text-sm" data-testid={`${testId}-panel`}>
          {explanation ? (
            <>
              <p className="whitespace-pre-wrap text-brand-slate-700">{explanation}</p>
              {explanations.disclaimer && (
                <p className="mt-2 text-xs text-brand-slate-400">{explanations.disclaimer}</p>
              )}
            </>
          ) : explanations.isLoading ? (
            <span className="flex items-center gap-2 text-brand-slate-500">
              <Spinner size="sm" /> Explaining…
            </span>
          ) : explanations.error ? (
            <div role="alert">
              <Notice variant="error" title={explanations.error} />
            </div>
          ) : (
            <p className="text-brand-slate-500">No explanation available for this item.</p>
          )}
        </div>
      )}
    </div>
  );
}
