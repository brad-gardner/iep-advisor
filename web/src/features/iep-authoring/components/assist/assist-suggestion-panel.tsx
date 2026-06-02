import { Button } from '@/components/ui/button';

interface AssistSuggestionPanelProps {
  suggestion: string;
  // Omit to render the suggestion read-only (display-only, Dismiss only).
  onAccept?: () => void;
  onDismiss: () => void;
  testIdPrefix: string;
}

// Renders a returned suggestion with Accept (optional) + Dismiss.
export function AssistSuggestionPanel({
  suggestion,
  onAccept,
  onDismiss,
  testIdPrefix,
}: AssistSuggestionPanelProps) {
  return (
    <div
      className="mt-2 rounded-card border border-brand-teal-100 bg-brand-teal-50 p-3"
      data-testid={`${testIdPrefix}-suggestion`}
    >
      <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-brand-slate-700">
        {suggestion}
      </p>
      <div className="mt-3 flex items-center justify-end gap-2">
        {onAccept && (
          <Button
            variant="primary"
            className="px-3 py-1.5"
            onClick={onAccept}
            data-testid={`${testIdPrefix}-accept`}
          >
            Accept
          </Button>
        )}
        <Button
          variant="ghost"
          className="px-3 py-1.5"
          onClick={onDismiss}
          data-testid={`${testIdPrefix}-dismiss`}
        >
          Dismiss
        </Button>
      </div>
    </div>
  );
}
