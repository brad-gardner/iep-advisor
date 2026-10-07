import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAutosave } from '@/hooks/use-autosave';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { fieldElementId, type FieldRendererProps } from './types';

/** Boolean checkbox. Stored as a `true`/`false` value. */
export function CheckboxField({ field, value, disabled, onSave }: FieldRendererProps) {
  const { t } = useTranslation(['document-authoring', 'common']);
  const id = fieldElementId(field.id);
  const [checked, setChecked] = useState(value === true);
  // Discrete toggle → save immediately.
  const autosave = useAutosave<boolean>(
    useCallback(async (v) => void (await onSave({ [field.fieldKey]: v })), [field.fieldKey, onSave]),
    { delay: 0 }
  );
  useRegisterFlush(field.fieldKey, autosave.flush);

  const handleToggle = (next: boolean) => {
    setChecked(next);
    autosave.save(next);
  };

  return (
    <div className="flex items-center gap-2">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={(e) => handleToggle(e.target.checked)}
        className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-500"
        data-testid={`field-${field.fieldKey}`}
      />
      <label htmlFor={id} className="text-[13px] font-medium text-brand-slate-600">
        {field.label || t('fieldLabel.untitled')}
        {field.required && (
          <span className="ml-1 text-brand-danger-700" aria-hidden="true">
            *
          </span>
        )}
        {field.required && <span className="sr-only"> {t('common:ui.required')}</span>}
      </label>
    </div>
  );
}
