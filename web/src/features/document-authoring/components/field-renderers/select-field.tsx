import { useCallback, useState } from 'react';
import { Select } from '@/components/ui/input';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import { parseConfig } from '@/features/admin/templates/template-config';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';

/** Single-select dropdown populated from the field's config options. */
export function SelectField({ field, value, disabled, onSave }: FieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const options = config.kind === 'Select' ? config.select.options : [];
  const id = fieldElementId(field.id);

  const [local, setLocal] = useState(typeof value === 'string' ? value : '');
  // Selects fire discrete changes, so save immediately (no debounce lag).
  const autosave = useAutosave<string>(
    useCallback(async (v) => void (await onSave({ [field.fieldKey]: v })), [field.fieldKey, onSave]),
    { delay: 0 }
  );
  useRegisterFlush(field.fieldKey, autosave.flush);

  const handleChange = (next: string) => {
    setLocal(next);
    autosave.save(next);
  };

  return (
    <div>
      <FieldLabel htmlFor={id} label={field.label} required={field.required} />
      <Select
        id={id}
        value={local}
        disabled={disabled}
        onChange={(e) => handleChange(e.target.value)}
        data-testid={`field-${field.fieldKey}`}
      >
        <option value="">Select…</option>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label?.trim() || o.value}
          </option>
        ))}
      </Select>
    </div>
  );
}
