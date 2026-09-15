import { useCallback, useState } from 'react';
import { Input } from '@/components/ui/input';
import { useAutosave } from '@/hooks/use-autosave';
import { parseConfig } from '@/features/admin/templates/template-config';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';
import { FieldAssistBar } from './field-assist-bar';
import { useDocumentEditorContext } from '../../hooks/document-editor-context';

/** Single-line Text field. Honors the config `maxLength`. */
export function TextField({ field, value, disabled, onSave }: FieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const maxLength = config.kind === 'Text' ? config.text.maxLength : undefined;
  const id = fieldElementId(field.id);

  const [local, setLocal] = useState(typeof value === 'string' ? value : '');
  const editor = useDocumentEditorContext();
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
      <Input
        id={id}
        type="text"
        value={local}
        disabled={disabled}
        maxLength={maxLength}
        onChange={(e) => handleChange(e.target.value)}
        onFocus={() =>
          editor?.setActiveField({
            label: field.label || 'this field',
            apply: (text) => {
              handleChange(text);
              void autosave.flush();
            },
          })
        }
        onBlur={() => void autosave.flush()}
        data-testid={`field-${field.fieldKey}`}
      />
      <FieldAssistBar
        fieldKey={field.fieldKey}
        kinds={['Rewrite', 'Improve']}
        onApply={(text) => {
          handleChange(maxLength != null ? text.slice(0, maxLength) : text);
          void autosave.flush();
        }}
        beforeRequest={autosave.flush}
        disabled={disabled}
        testIdPrefix={`field-${field.fieldKey}`}
      />
    </div>
  );
}
