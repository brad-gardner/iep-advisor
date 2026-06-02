import { useState } from 'react';
import { Sparkles } from 'lucide-react';
import { Notice } from '@/components/ui/notice';
import type { ApiResponse } from '@/types/api';
import type { AssistKind, AssistResponse } from '../../api/iep-assist-types';
import { useFieldAssist } from '../../hooks/use-field-assist';
import { AssistKindMenu } from './assist-kind-menu';
import { AssistSpinner } from './assist-spinner';
import { AssistSuggestionPanel } from './assist-suggestion-panel';

interface AssistPopoverProps {
  // API call bound to a specific row (goal/section/service line).
  requestFn: (kind: AssistKind) => Promise<ApiResponse<AssistResponse>>;
  // Kinds to offer for this field. Defaults to all three.
  kinds?: AssistKind[];
  // Apply the accepted text to the field. Omit for display-only assist
  // (e.g. service lines have no single obvious target field).
  onApply?: (text: string) => void;
  testIdPrefix: string;
}

const ALL_KINDS: AssistKind[] = ['Rewrite', 'Improve', 'SuggestMeasurement'];

// Small inline "AI help" affordance: a trigger button that opens a kind menu,
// then surfaces loading / suggestion / error states beneath the field.
export function AssistPopover({
  requestFn,
  kinds = ALL_KINDS,
  onApply,
  testIdPrefix,
}: AssistPopoverProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const assist = useFieldAssist(requestFn);

  const handlePick = (kind: AssistKind) => {
    setMenuOpen(false);
    assist.request(kind);
  };

  const handleAccept = () => {
    if (!onApply) return;
    assist.accept(onApply);
  };

  return (
    <div className="space-y-2">
      <div className="relative inline-block">
        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          disabled={assist.status === 'loading'}
          className="inline-flex items-center gap-1.5 rounded-button border border-brand-teal-200 px-2.5 py-1 text-[13px] font-medium text-brand-teal-600 transition-colors hover:bg-brand-teal-50 disabled:opacity-50"
          data-testid={`${testIdPrefix}-button`}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
        >
          <Sparkles className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          AI help
        </button>
        {menuOpen && (
          <AssistKindMenu kinds={kinds} onPick={handlePick} testIdPrefix={testIdPrefix} />
        )}
      </div>

      {assist.status === 'loading' && <AssistSpinner label="Generating suggestion…" />}

      {assist.status === 'suggested' && assist.suggestion !== null && (
        <AssistSuggestionPanel
          suggestion={assist.suggestion}
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
  );
}
