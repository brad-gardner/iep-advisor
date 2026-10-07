import { useTranslation } from 'react-i18next';
import { Check, X } from 'lucide-react';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

/** Read view for a Checkbox field: a Yes/No line (booleans are never "blank"). */
export function ReadCheckbox({ field, value }: ReadFieldRendererProps) {
  const { t } = useTranslation('document-authoring');
  const checked = value === true;
  return (
    <div
      id={fieldElementId(field.id)}
      className="flex items-center gap-2"
      data-testid={`read-field-${field.fieldKey}`}
    >
      {checked ? (
        <Check className="h-4 w-4 shrink-0 text-brand-teal-600" aria-hidden="true" />
      ) : (
        <X className="h-4 w-4 shrink-0 text-brand-slate-500" aria-hidden="true" />
      )}
      <p className="text-[15px] text-brand-slate-700">
        {field.label || t('readShared.untitledField')} — {checked ? t('readCheckbox.yes') : t('readCheckbox.no')}
      </p>
    </div>
  );
}
