import { useCallback, useEffect, useRef, useState } from 'react';
import { Input } from '@/components/ui/input';
import { useAutosave } from '@/hooks/use-autosave';
import { parseConfig } from '@/features/admin/templates/template-config';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';
import { FieldAssistBar } from './field-assist-bar';
import { appendText, useDocumentEditorContext } from '../../hooks/document-editor-context';

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

  // Programmatic writes (AI apply, evidence insert) share one path: clip to
  // maxLength — the attribute only limits typing — then persist at once.
  const applyText = (text: string) => {
    handleChange(maxLength != null ? text.slice(0, maxLength) : text);
    void autosave.flush();
  };

  // The evidence-insert target may be invoked after this render: read the
  // latest value/disabled state through refs and drop the target on unmount.
  const localRef = useRef(local);
  const disabledRef = useRef(disabled);
  useEffect(() => {
    localRef.current = local;
    disabledRef.current = disabled;
  }, [local, disabled]);
  const clear = editor?.clearActiveField;
  useEffect(() => () => clear?.(field.fieldKey), [clear, field.fieldKey]);

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
            id: field.fieldKey,
            label: () => field.label || 'this field',
            apply: (text) => {
              if (disabledRef.current) return;
              applyText(appendText(localRef.current, text, ' '));
            },
          })
        }
        onBlur={() => void autosave.flush()}
        data-testid={`field-${field.fieldKey}`}
      />
      <FieldAssistBar
        fieldKey={field.fieldKey}
        kinds={['Rewrite', 'Improve']}
        onApply={applyText}
        beforeRequest={autosave.flush}
        disabled={disabled}
        testIdPrefix={`field-${field.fieldKey}`}
      />
    </div>
  );
}
