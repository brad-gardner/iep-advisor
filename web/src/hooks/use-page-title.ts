import { useEffect } from 'react';

/**
 * Sets the browser tab title for the current page: `"<title> · IEP Advisor"`.
 * Call unconditionally at the top of every routed page component — before any
 * early return — so loading/guard states get a title too. An empty/undefined
 * title (e.g. while identity-bearing data is still loading) falls back to the
 * bare app name rather than a blank tab.
 */
export function usePageTitle(title: string | null | undefined): void {
  useEffect(() => {
    document.title = title ? `${title} · IEP Advisor` : 'IEP Advisor';
  }, [title]);
}
