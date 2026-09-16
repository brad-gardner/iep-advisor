import { useEffect } from 'react';

/**
 * Sets the browser tab title for the current page: `"<title> · IEP Advisor"`.
 * Call unconditionally at the top of every routed page component — before any
 * early return — so loading/guard states get a title too. An empty/undefined
 * title (e.g. while identity-bearing data is still loading) falls back to the
 * bare app name rather than a blank tab.
 *
 * Restores whatever `document.title` was before this effect ran when it
 * unmounts or the title changes. Every call site today is a leaf routed page,
 * so this is a no-op in practice (the next page's own `usePageTitle` already
 * overwrites it); it only matters the first time this hook is used inside a
 * component that can unmount while a persistent parent (a modal, a nested
 * detail route) remains mounted — without it, that parent's title would be
 * left permanently overwritten.
 */
export function usePageTitle(title: string | null | undefined): void {
  useEffect(() => {
    const previous = document.title;
    document.title = title ? `${title} · IEP Advisor` : 'IEP Advisor';
    return () => {
      document.title = previous;
    };
  }, [title]);
}
