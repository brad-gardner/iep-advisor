import { useEffect, useState } from 'react';
import { cn } from '@/lib/cn';
import type { TemplateSectionDto } from '../types';
import { sectionDomId } from '../lib/section-dom';

interface SectionNavigatorProps {
  sections: TemplateSectionDto[];
}

/**
 * Sticky section rail for the editor. Scroll-spy highlights the section in
 * view; `[` / `]` (handled by the editor) step between sections. Collapses to
 * a horizontal chip row below `lg`.
 */
export function SectionNavigator({ sections }: SectionNavigatorProps) {
  const [activeId, setActiveId] = useState<number | null>(sections[0]?.id ?? null);

  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined' || sections.length === 0) return;
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
    for (const s of sections) {
      const el = document.getElementById(sectionDomId(s.id));
      if (el) observer.observe(el);
    }
    return () => observer.disconnect();
  }, [sections]);

  if (sections.length === 0) return null;

  return (
    <nav
      aria-label="Document sections"
      className="lg:sticky lg:top-4 lg:self-start"
      data-testid="section-navigator"
    >
      <ol className="flex gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
        {sections.map((s, i) => (
          <li key={s.id} className="shrink-0">
            <a
              href={`#${sectionDomId(s.id)}`}
              onClick={(e) => {
                e.preventDefault();
                document.getElementById(sectionDomId(s.id))?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                setActiveId(s.id);
              }}
              aria-current={activeId === s.id ? 'location' : undefined}
              className={cn(
                'block rounded-button px-3 py-1.5 text-[13px] transition-colors',
                activeId === s.id
                  ? 'bg-brand-teal-50 font-medium text-brand-teal-700'
                  : 'text-brand-slate-600 hover:bg-brand-slate-100'
              )}
              data-testid={`section-nav-${s.id}`}
            >
              <span className="mr-1.5 text-brand-slate-400">{i + 1}</span>
              {s.title || 'Untitled section'}
            </a>
          </li>
        ))}
      </ol>
      <p className="mt-2 hidden text-xs text-brand-slate-400 lg:block">
        Press <kbd className="rounded border px-1">[</kbd> / <kbd className="rounded border px-1">]</kbd> to move between sections
      </p>
    </nav>
  );
}
