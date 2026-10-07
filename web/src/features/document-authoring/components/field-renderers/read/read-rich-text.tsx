import { useTranslation } from 'react-i18next';
import { Markdown } from '@/components/ui/markdown';
import { isBlank } from '../../../lib/completeness';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

/**
 * Read view for a RichText field: the stored markdown rendered the same way
 * the rest of the app displays it (`Markdown`, sanitized — never raw HTML),
 * clamped to a comfortable reading width.
 */
export function ReadRichText({ field, value, hideLabel }: ReadFieldRendererProps) {
  const { t } = useTranslation(['document-authoring', 'common']);
  const content = typeof value === 'string' ? value : '';
  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      {!hideLabel && <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || t('readShared.untitledField')}</h3>}
      {isBlank(content) ? (
        <p className="mt-0.5 text-[15px] text-brand-slate-500 italic">{t('common:ui.notSet')}</p>
      ) : (
        <div className="mt-1 max-w-[75ch] text-[15px] leading-relaxed text-brand-slate-700">
          <Markdown content={content} />
        </div>
      )}
    </div>
  );
}
