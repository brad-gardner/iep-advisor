import { useCallback, useEffect, useState } from 'react';
import { sectionDomId } from '../lib/section-dom';

/**
 * Single owner of "which section is the user in". Scroll-spy via one
 * IntersectionObserver over the section cards; the navigator highlights it and
 * the `[` / `]` shortcut steps relative to it, so the two never disagree.
 * `setActive` lets an explicit jump update the highlight immediately.
 */
export function useActiveSection(sectionIds: number[]): {
  activeId: number | null;
  setActive: (id: number) => void;
} {
  const [activeId, setActiveId] = useState<number | null>(sectionIds[0] ?? null);

  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined' || sectionIds.length === 0) return;
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible.length > 0) {
          const id = Number(visible[0].target.id.replace('document-section-', ''));
          if (Number.isFinite(id)) setActiveId(id);
        }
      },
      { rootMargin: '-10% 0px -70% 0px', threshold: [0, 1] }
    );
    for (const id of sectionIds) {
      const el = document.getElementById(sectionDomId(id));
      if (el) observer.observe(el);
    }
    return () => observer.disconnect();
  }, [sectionIds]);

  const setActive = useCallback((id: number) => setActiveId(id), []);
  return { activeId, setActive };
}

/** Pure stepping helper (exported for tests): the neighbour of `activeId`. */
export function stepSection(sectionIds: number[], activeId: number | null, direction: 1 | -1): number | null {
  if (sectionIds.length === 0) return null;
  const idx = activeId == null ? -1 : sectionIds.indexOf(activeId);
  const nextIdx = idx < 0 ? 0 : Math.min(sectionIds.length - 1, Math.max(0, idx + direction));
  return sectionIds[nextIdx];
}
