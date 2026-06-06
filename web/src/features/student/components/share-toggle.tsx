import { Eye, Lock } from 'lucide-react';
import type { StudentWorkspaceEntryDto } from '../types';

interface ShareToggleProps {
  entry: StudentWorkspaceEntryDto;
  onToggle: (id: number, isShareable: boolean) => void;
  testIdPrefix: string;
}

// A clear private/shared switch. Uses a button with aria-pressed so the state is
// announced and keyboard-operable. The label spells out the consequence.
export function ShareToggle({ entry, onToggle, testIdPrefix }: ShareToggleProps) {
  const { isShareable } = entry;

  return (
    <button
      type="button"
      onClick={() => onToggle(entry.id, !isShareable)}
      aria-pressed={isShareable}
      className={`inline-flex items-center gap-1.5 rounded-button border px-2.5 py-1 text-[13px] font-medium transition-colors ${
        isShareable
          ? 'border-brand-teal-200 bg-brand-teal-50 text-brand-teal-600 hover:bg-brand-teal-100'
          : 'border-brand-slate-200 bg-white text-brand-slate-500 hover:bg-brand-slate-100'
      }`}
      data-testid={`${testIdPrefix}-share-toggle`}
    >
      {isShareable ? (
        <>
          <Eye className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          Shared with your team
        </>
      ) : (
        <>
          <Lock className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          Only you can see this
        </>
      )}
    </button>
  );
}
