import { useCallback, useState } from 'react';
import { Textarea } from '@/components/ui/input';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';

/**
 * RichText field. The backend sanitizes RichText to an allowlist on save; for
 * now we render a plain multiline Textarea (no WYSIWYG) — the stored value is
 * still a string.
 */
export function RichTextField({ field, value, disabled, onSave }: FieldRendererProps) {
  const id = fieldElementId(field.id);
  const [local, setLocal] = useState(typeof value === 'string' ? value : '');
  const autosave = useAutosave<string>(
    useCallback(async (v) => void (await onSave({ [field.fieldKey]: v })), [field.fieldKey, onSave])
  );
  useRegisterFlush(field.fieldKey, autosave.flush);

  const handleChange = (next: string) => {
    setLocal(next);
    autosave.save(next);
  };

  return (
    <div>
      <FieldLabel htmlFor={id} label={field.label} required={field.required} />
      <Textarea
        id={id}
        rows={4}
        value={local}
        disabled={disabled}
        onChange={(e) => handleChange(e.target.value)}
        onBlur={() => void autosave.flush()}
        data-testid={`field-${field.fieldKey}`}
      />
    </div>
  );
}
