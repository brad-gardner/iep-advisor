import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { MapPin, X } from 'lucide-react';
import { stateHintCopy } from '../lib/copy';

interface StateHintProps {
  /** Scopes the "don't show again" choice to this account on this device. */
  userId: number;
}

const storageKey = (userId: number) => `iep-advisor:advocate:state-hint-dismissed:${userId}`;

function readDismissed(userId: number): boolean {
  try {
    return localStorage.getItem(storageKey(userId)) === '1';
  } catch {
    return false;
  }
}

/**
 * Shown above the composer while the parent's profile has no state: without
 * one the advocate can only cite federal rules. Dismissal is remembered per
 * account in this browser; setting the state removes it everywhere.
 */
export function StateHint({ userId }: StateHintProps) {
  const { t } = useTranslation('advocate');
  const [dismissed, setDismissed] = useState(() => readDismissed(userId));
  if (dismissed) return null;

  const dismiss = () => {
    setDismissed(true);
    try {
      localStorage.setItem(storageKey(userId), '1');
    } catch {
      // Private mode: the hint just comes back next visit.
    }
  };

  return (
    <div
      className="flex items-start gap-2 rounded-card border border-brand-amber-100 bg-brand-amber-50 px-3 py-2 text-xs text-brand-slate-700"
      role="status"
      data-testid="advocate-state-hint"
    >
      <MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0 text-brand-amber-500" strokeWidth={1.8} aria-hidden="true" />
      <p className="min-w-0 flex-1">
        <Link to="/profile" className="font-medium text-brand-teal-600 underline underline-offset-2 hover:text-brand-teal-700">
          {stateHintCopy()}
        </Link>
        {t('stateHint.body')}
      </p>
      <button
        type="button"
        onClick={dismiss}
        aria-label={t('stateHint.dismissAria')}
        className="shrink-0 rounded p-0.5 text-brand-slate-400 hover:bg-brand-amber-100 hover:text-brand-slate-600 focus:outline-none focus:ring-1 focus:ring-brand-teal-500"
        data-testid="advocate-state-hint-dismiss"
      >
        <X className="h-3.5 w-3.5" aria-hidden="true" />
      </button>
    </div>
  );
}
