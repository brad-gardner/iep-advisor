import { isBlank } from '../../../lib/completeness';
import { fieldElementId } from '../types';
import { NOT_SET_LABEL, type ReadFieldRendererProps } from './types';

/** Read view for a Text field: the label plus its plain-text value. */
export function ReadText({ field, value, hideLabel }: ReadFieldRendererProps) {
  const str = typeof value === 'string' ? value : '';
  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      {!hideLabel && <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>}
      {isBlank(str) ? (
        <p className="mt-0.5 text-[15px] text-brand-slate-500 italic">{NOT_SET_LABEL}</p>
      ) : (
        <p className="mt-0.5 max-w-[75ch] text-[15px] leading-relaxed text-brand-slate-700">{str}</p>
      )}
    </div>
  );
}
