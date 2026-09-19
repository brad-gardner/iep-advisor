import { X } from 'lucide-react';
import { aboutContextLabel, type AboutRef } from '../lib/about';

interface AboutContextPillProps {
  about: AboutRef;
  /** Human name from the launcher (router state), e.g. "IEP from Mar 3, 2026". */
  label?: string | null;
  /** Drops the context so the next question is a general one. */
  onClear?: () => void;
}

/**
 * "About: IEP from March 2026" — the record a launcher opened this fresh
 * conversation about. Derived client-side; the raw `about` token is never shown.
 */
export function AboutContextPill({ about, label, onClear }: AboutContextPillProps) {
  return (
    <div className="flex items-center gap-2" data-testid="advocate-about-pill">
      <span className="inline-flex max-w-full items-center gap-1 rounded-badge border border-brand-teal-100 bg-brand-teal-50 px-2.5 py-1 text-xs font-medium text-brand-teal-600">
        <span className="truncate">{aboutContextLabel(about, label)}</span>
        {onClear && (
          <button
            type="button"
            onClick={onClear}
            aria-label="Clear the conversation context"
            className="ml-0.5 rounded-full p-0.5 text-brand-teal-500 hover:bg-brand-teal-100 focus:outline-none focus:ring-1 focus:ring-brand-teal-400"
            data-testid="advocate-about-clear"
          >
            <X className="h-3 w-3" aria-hidden="true" />
          </button>
        )}
      </span>
      <span className="text-xs text-brand-slate-400">Your first question starts a new conversation about it.</span>
    </div>
  );
}
