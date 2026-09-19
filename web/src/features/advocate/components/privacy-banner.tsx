import { Lock } from 'lucide-react';
import { PRIVACY_BANNER_COPY } from '../lib/copy';

/**
 * The privacy boundary, stated on the surface itself (persona rule: every
 * private screen says so). Quiet by design — it is a standing fact, not an
 * alert.
 */
export function PrivacyBanner() {
  return (
    <p
      className="flex items-start gap-2 rounded-card border border-brand-slate-200 bg-brand-slate-50 px-3 py-2 text-xs text-brand-slate-600"
      data-testid="advocate-privacy-banner"
    >
      <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />
      <span>{PRIVACY_BANNER_COPY}</span>
    </p>
  );
}
