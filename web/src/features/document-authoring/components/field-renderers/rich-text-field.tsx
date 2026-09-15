import { useCallback, useEffect, useRef, useState } from 'react';
import { Textarea } from '@/components/ui/input';
import { useAutosave } from '@/hooks/use-autosave';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';
import { FieldAssistBar } from './field-assist-bar';
import { appendText, useDocumentEditorContext } from '../../hooks/document-editor-context';

/**
 * RichText field. The backend sanitizes RichText to an allowlist on save; for
 * now we render a plain multiline Textarea (no WYSIWYG) — the stored value is
 * still a string.
 */
export function RichTextField({ field, value, disabled, onSave }: FieldRendererProps) {
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

  const applyText = (text: string) => {
    handleChange(text);
    void autosave.flush();
  };

  // See TextField: the evidence-insert target outlives this render, so it
  // reads through refs and is dropped on unmount.
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
      <Textarea
        id={id}
        rows={4}
        value={local}
        disabled={disabled}
        onChange={(e) => handleChange(e.target.value)}
        onFocus={() =>
          editor?.setActiveField({
            id: field.fieldKey,
            label: () => field.label || 'this field',
            apply: (text) => {
              if (disabledRef.current) return;
              applyText(appendText(localRef.current, text));
            },
          })
        }
        onBlur={() => void autosave.flush()}
        data-testid={`field-${field.fieldKey}`}
      />
      <FieldAssistBar
        fieldKey={field.fieldKey}
        onApply={applyText}
        allowPull
        beforeRequest={autosave.flush}
        disabled={disabled}
        testIdPrefix={`field-${field.fieldKey}`}
      />
    </div>
  );
}
