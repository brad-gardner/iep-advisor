import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, ChevronDown } from 'lucide-react';
import { cn } from '@/lib/cn';
import { Spinner } from '@/components/ui/spinner';
import type { CompletenessItem, CompletenessSummary } from '../lib/completeness';
import { jumpToField } from '../lib/section-dom';
import { fieldElementId } from './field-renderers/types';

interface CompletenessStripProps {
  summary: CompletenessSummary;
  /** True while an open section has a save in flight — the strip reflects
   *  saved values, so this tells the educator a percent/flag may be stale. */
  updating: boolean;
}

/**
 * Completeness moved out of the right rail into a strip under the header:
 * percent + bar + required/advisory counts, with a "Show items" disclosure
 * that lists every flagged item and jumps to it. Required gaps are what
 * finalize will reject; advisory items are coaching and never block.
 */
export function CompletenessStrip({ summary, updating }: CompletenessStripProps) {
  const { t } = useTranslation('document-authoring');
  const [itemsOpen, setItemsOpen] = useState(false);
  const required = summary.items.filter((i) => i.severity === 'required');
  const advisory = summary.items.filter((i) => i.severity === 'advisory');

  const jump = (item: CompletenessItem) => jumpToField(fieldElementId(item.fieldId), item.sectionId);

  return (
    <section
      className="rounded-card border border-brand-slate-200 bg-white"
      aria-label={t('completenessStrip.ariaLabel')}
      data-testid="completeness-strip"
    >
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 px-5 py-3">
        <div className="flex items-baseline gap-2">
          <h2 className="font-serif text-lg text-brand-slate-800">{t('completenessStrip.heading')}</h2>
          <span className="text-2xl font-semibold text-brand-teal-600" data-testid="completeness-percent">
            {summary.percent}%
          </span>
          {updating && (
            <span
              className="inline-flex items-center gap-1 text-xs text-brand-slate-500"
              role="status"
              data-testid="completeness-updating"
            >
              <Spinner size="sm" tone="current" className="h-3 w-3" aria-hidden="true" />
              {t('completenessStrip.updating')}
            </span>
          )}
        </div>
        <div className="h-2 min-w-[12rem] flex-1 overflow-hidden rounded-full bg-brand-slate-100" aria-hidden="true">
          <div className="h-2 rounded-full bg-brand-teal-500 transition-all" style={{ width: `${summary.percent}%` }} />
        </div>
        {summary.items.length === 0 ? (
          <p className="flex items-center gap-1.5 text-sm text-brand-slate-600">
            <CheckCircle2 className="h-4 w-4 text-brand-teal-500" aria-hidden="true" />
            {t('completenessStrip.nothingFlagged')}
          </p>
        ) : (
          <>
            {required.length > 0 && (
              <span className="inline-flex items-center gap-1.5 text-sm text-brand-slate-700">
                <span className="h-2 w-2 rounded-full bg-brand-danger-500" aria-hidden="true" />
                {t('completenessStrip.requiredCount', { count: required.length })}
              </span>
            )}
            {advisory.length > 0 && (
              <span className="inline-flex items-center gap-1.5 text-sm text-brand-slate-700">
                <span className="h-2 w-2 rounded-full bg-brand-amber-400" aria-hidden="true" />
                {t('completenessStrip.advisoryCount', { count: advisory.length })}
              </span>
            )}
            <button
              type="button"
              onClick={() => setItemsOpen((v) => !v)}
              aria-expanded={itemsOpen}
              className="ml-auto inline-flex items-center gap-1 text-sm text-brand-teal-600 hover:underline"
              data-testid="completeness-show-items"
            >
              {t('completenessStrip.showItems')}
              <ChevronDown className={cn('h-4 w-4 transition-transform', itemsOpen && 'rotate-180')} aria-hidden="true" />
            </button>
          </>
        )}
      </div>

      {itemsOpen && summary.items.length > 0 && (
        <ul
          className="grid gap-2 border-t border-brand-slate-100 px-5 py-3 text-sm sm:grid-cols-2"
          data-testid="completeness-items"
        >
          {[...required, ...advisory].map((item) => (
            <li key={item.key}>
              <button
                type="button"
                onClick={() => jump(item)}
                className={
                  item.severity === 'required'
                    ? 'text-left text-brand-danger-700 hover:underline'
                    : 'text-left text-brand-amber-600 hover:underline'
                }
                data-testid={`completeness-item-${item.key}`}
              >
                {item.severity === 'required' ? t('completenessStrip.requiredPrefix') : t('completenessStrip.advisoryPrefix')}
                {item.message}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
