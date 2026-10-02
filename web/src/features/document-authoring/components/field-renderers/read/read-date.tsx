import { formatDate } from '@/lib/format-date';
import { isBlank } from '../../../lib/completeness';
import { fieldElementId } from '../types';
import { NOT_SET_LABEL, type ReadFieldRendererProps } from './types';

/** Read view for a Date field: the stored ISO date, formatted for display. */
export function ReadDate({ field, value }: ReadFieldRendererProps) {
  const str = typeof value === 'string' ? value : '';
  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
      <p className={isBlank(str) ? 'mt-0.5 text-[15px] italic text-brand-slate-500' : 'mt-0.5 text-[15px] text-brand-slate-700'}>
        {isBlank(str) ? NOT_SET_LABEL : formatDate(str)}
      </p>
    </div>
  );
}
