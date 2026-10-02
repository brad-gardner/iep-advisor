import { parseConfig } from '@/features/admin/templates/template-config';
import { isBlank } from '../../../lib/completeness';
import { fieldElementId } from '../types';
import { NOT_SET_LABEL, type ReadFieldRendererProps } from './types';

/** Read view for a Select field: the chosen option's label (falling back to
 *  its raw value if the option was since removed from the config). */
export function ReadSelect({ field, value }: ReadFieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const options = config.kind === 'Select' ? config.select.options : [];
  const str = typeof value === 'string' ? value : '';
  const label = options.find((o) => o.value === str)?.label?.trim() || str;

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
      <p className={isBlank(str) ? 'mt-0.5 text-[15px] italic text-brand-slate-500' : 'mt-0.5 text-[15px] text-brand-slate-700'}>
        {isBlank(str) ? NOT_SET_LABEL : label}
      </p>
    </div>
  );
}
