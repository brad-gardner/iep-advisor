import { Trans, useTranslation } from 'react-i18next';
import { cn } from '@/lib/cn';
import type { TemplateSectionDto } from '../types';
import { jumpToSection, sectionDomId } from '../lib/section-dom';

interface SectionNavigatorProps {
  sections: TemplateSectionDto[];
  activeId: number | null;
  onJump: (sectionId: number) => void;
}

/**
 * Sticky section rail for the editor. The active section is owned by the
 * editor (`useActiveSection`); activating a link scrolls to AND focuses the
 * section card so keyboard users land inside it. Collapses to a horizontal
 * chip row below `lg`.
 */
export function SectionNavigator({ sections, activeId, onJump }: SectionNavigatorProps) {
  const { t } = useTranslation('document-authoring');
  if (sections.length === 0) return null;

  return (
    <nav aria-label={t('sectionNavigator.ariaLabel')} className="lg:sticky lg:top-4 lg:self-start" data-testid="section-navigator">
      <ol className="flex gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
        {sections.map((s, i) => (
          <li key={s.id} className="shrink-0">
            <a
              href={`#${sectionDomId(s.id)}`}
              onClick={(e) => {
                e.preventDefault();
                jumpToSection(s.id);
                onJump(s.id);
              }}
              aria-current={activeId === s.id ? 'location' : undefined}
              className={cn(
                'block rounded-button px-3 py-1.5 text-[13px] transition-colors',
                activeId === s.id ? 'bg-brand-teal-50 font-medium text-brand-teal-700' : 'text-brand-slate-600 hover:bg-brand-slate-100'
              )}
              data-testid={`section-nav-${s.id}`}
            >
              <span className="mr-1.5 text-brand-slate-500">{i + 1}</span>
              {s.title || t('sectionCard.untitledSection')}
            </a>
          </li>
        ))}
      </ol>
      <p className="mt-2 hidden text-xs text-brand-slate-500 lg:block">
        <Trans
          t={t}
          i18nKey="sectionNavigator.shortcutHint"
          components={{
            bracketOpen: <kbd className="rounded border px-1" />,
            bracketClose: <kbd className="rounded border px-1" />,
            editKey: <kbd className="rounded border px-1" />,
          }}
        />
      </p>
    </nav>
  );
}
