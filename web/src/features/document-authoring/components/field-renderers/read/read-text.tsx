import { useTranslation } from 'react-i18next';
import { isBlank } from '../../../lib/completeness';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

/** Read view for a Text field: the label plus its plain-text value. */
export function ReadText({ field, value, hideLabel }: ReadFieldRendererProps) {
  const { t } = useTranslation(['document-authoring', 'common']);
  const str = typeof value === 'string' ? value : '';
  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      {!hideLabel && <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || t('readShared.untitledField')}</h3>}
      {isBlank(str) ? (
        <p className="mt-0.5 text-[15px] text-brand-slate-500 italic">{t('common:ui.notSet')}</p>
      ) : (
        <p className="mt-0.5 max-w-[75ch] text-[15px] leading-relaxed text-brand-slate-700">{str}</p>
      )}
    </div>
  );
}
