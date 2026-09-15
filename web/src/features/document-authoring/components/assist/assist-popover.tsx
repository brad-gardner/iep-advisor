import { useCallback } from 'react';
import { Sparkles } from 'lucide-react';
import { Menu } from '@/components/ui/menu';
import { Notice } from '@/components/ui/notice';
import type { ApiResponse } from '@/types/api';
import { ASSIST_KIND_LABELS, type AssistKind, type AssistResponse } from '../../api/assist-types';
import { useFieldAssist } from '../../hooks/use-field-assist';
import { AssistSpinner } from './assist-spinner';
import { AssistSuggestionPanel } from './assist-suggestion-panel';

interface AssistPopoverProps {
  // API call bound to a specific field / row.
  requestFn: (kind: AssistKind) => Promise<ApiResponse<AssistResponse>>;
  // Kinds to offer for this field. Defaults to all three.
  kinds?: AssistKind[];
  // Apply the accepted text to the field. Omit for display-only assist.
  onApply?: (text: string) => void;
  // Awaited before the request (e.g. flush the field's pending autosave) so the
  // server coaches the text the user sees, not the last persisted value.
  beforeRequest?: () => Promise<void>;
  testIdPrefix: string;
}

const ALL_KINDS: AssistKind[] = ['Rewrite', 'Improve', 'SuggestMeasurement'];

// Inline "AI help" affordance: the design-system Menu (APG menu-button with
// arrow keys, Esc + return-focus, click-outside) picks a kind, then the
// loading / suggestion / error states render beneath the field in a polite
// live region so assistive tech hears the suggestion arrive.
export function AssistPopover({ requestFn, kinds = ALL_KINDS, onApply, beforeRequest, testIdPrefix }: AssistPopoverProps) {
  const guardedRequest = useCallback(
    async (kind: AssistKind) => {
      if (beforeRequest) await beforeRequest();
      return requestFn(kind);
    },
    [beforeRequest, requestFn]
  );
  const assist = useFieldAssist(guardedRequest);

  const handleAccept = () => {
    if (!onApply) return;
    assist.accept(onApply);
  };

  return (
    <div className="space-y-2">
      <Menu
        label="AI help"
        align="left"
        triggerClassName="inline-flex items-center rounded-button border border-brand-teal-200 px-2.5 py-1 transition-colors hover:bg-brand-teal-50 disabled:opacity-50"
        data-testid={`${testIdPrefix}-button`}
        trigger={
          <span className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-teal-600">
            <Sparkles className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            AI help
          </span>
        }
        items={kinds.map((kind) => ({
          label: ASSIST_KIND_LABELS[kind],
          onSelect: () => assist.request(kind),
          disabled: assist.status === 'loading',
          'data-testid': `${testIdPrefix}-kind-${kind}`,
        }))}
      />

      <div aria-live="polite">
        {assist.status === 'loading' && <AssistSpinner label="Generating suggestion…" />}

        {assist.status === 'suggested' && assist.suggestion !== null && (
          <AssistSuggestionPanel
            suggestion={assist.suggestion}
            rationale={assist.rationale}
            citations={assist.citations}
            missingBaseline={assist.missingBaseline}
            onAccept={onApply ? handleAccept : undefined}
            onDismiss={assist.dismiss}
            testIdPrefix={testIdPrefix}
          />
        )}

        {assist.status === 'error' && assist.errorMessage && (
          <div data-testid={`${testIdPrefix}-error`}>
            <Notice variant="error" title="AI help failed">
              {assist.errorMessage}
            </Notice>
          </div>
        )}
      </div>
    </div>
  );
}
