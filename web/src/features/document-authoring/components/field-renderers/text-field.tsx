import { useCallback, useState } from 'react';
import { Input } from '@/components/ui/input';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import { parseConfig } from '@/features/admin/templates/template-config';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';

/** Single-line Text field. Honors the config `maxLength`. */
export function TextField({ field, value, disabled, onSave }: FieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const maxLength = config.kind === 'Text' ? config.text.maxLength : undefined;
  const id = fieldElementId(field.id);

  const [local, setLocal] = useState(typeof value === 'string' ? value : '');
  const autosave = useAutosave<string>(
    useCallback(async (v) => void (await onSave({ [field.fieldKey]: v })), [field.fieldKey, onSave])
  );

  const handleChange = (next: string) => {
    setLocal(next);
    autosave.save(next);
  };

  return (
    <div>
      <FieldLabel htmlFor={id} label={field.label} required={field.required} />
      <Input
        id={id}
        type="text"
        value={local}
        disabled={disabled}
        maxLength={maxLength}
        onChange={(e) => handleChange(e.target.value)}
        onBlur={() => void autosave.flush()}
        data-testid={`field-${field.fieldKey}`}
      />
    </div>
  );
}
