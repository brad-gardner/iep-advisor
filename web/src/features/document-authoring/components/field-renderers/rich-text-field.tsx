import { useCallback, useEffect, useRef, useState } from 'react';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { useAutosave } from '@/hooks/use-autosave';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { FieldLabel } from './field-label';
import { fieldElementId, type FieldRendererProps } from './types';
import { FieldAssistBar } from './field-assist-bar';
import { appendText, useDocumentEditorContext } from '../../hooks/document-editor-context';

/**
 * RichText field: a TipTap editor storing markdown. The backend sanitizes and
 * renders that markdown structurally (PDF export, display elsewhere) — the
 * stored value is still a plain string, just markdown instead of raw prose.
 * `FieldLabel` (shared across every field-renderer type) is the visible
 * label; `aria-label` on the editor covers the accessible name since the
 * editable surface is a `role="textbox"` div, not a native labelable control.
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
      {/* onFocus/onBlur live on this wrapper (not passed to RichTextEditor
          itself): React re-derives its synthetic focus/blur "bubble" from the
          fiber tree rather than native DOM bubbling (focus/blur don't bubble
          natively), so this fires whenever any descendant of the editor
          gains/loses focus — the real TipTap contenteditable in production,
          and the plain textarea the shared test mock renders in its place. */}
      <div
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
      >
        <RichTextEditor
          id={id}
          minRows={4}
          value={local}
          disabled={disabled}
          aria-label={field.label || 'this field'}
          onChange={handleChange}
          data-testid={`field-${field.fieldKey}`}
        />
      </div>
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
