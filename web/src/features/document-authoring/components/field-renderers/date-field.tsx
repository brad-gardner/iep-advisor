import { useCallback, useState } from 'react';
import { Input } from '@/components/ui/input';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import { parseConfig } from '@/features/admin/templates/template-config';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';

/** Date field (native date input). The config `format` is a display hint for
 *  the PDF (Phase 4); the stored value is an ISO `yyyy-mm-dd` string. */
export function DateField({ field, value, disabled, onSave }: FieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const format = config.kind === 'Date' ? config.date.format : undefined;
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
        type="date"
        value={local}
        disabled={disabled}
        title={format ? `Format: ${format}` : undefined}
        onChange={(e) => handleChange(e.target.value)}
        onBlur={() => void autosave.flush()}
        data-testid={`field-${field.fieldKey}`}
      />
    </div>
  );
}
