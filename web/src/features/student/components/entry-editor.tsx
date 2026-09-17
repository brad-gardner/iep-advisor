import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { RichTextEditor } from '@/components/ui/rich-text-editor';

interface EntryEditorProps {
  // Pre-fill when editing an existing entry; empty when adding.
  initialContent?: string;
  initialShareable?: boolean;
  placeholder?: string;
  submitLabel: string;
  onSubmit: (content: string, isShareable: boolean) => Promise<void> | void;
  onCancel: () => void;
  testIdPrefix: string;
}

// A small content textarea + "Share with my team" checkbox used for both adding
// and editing a workspace entry.
export function EntryEditor({
  initialContent = '',
  initialShareable = false,
  placeholder,
  submitLabel,
  onSubmit,
  onCancel,
  testIdPrefix,
}: EntryEditorProps) {
  const [content, setContent] = useState(initialContent);
  const [isShareable, setIsShareable] = useState(initialShareable);
  const [saving, setSaving] = useState(false);

  const trimmed = content.trim();
  const checkboxId = `${testIdPrefix}-shareable`;

  const handleSubmit = async () => {
    if (!trimmed || saving) return;
    setSaving(true);
    try {
      await onSubmit(trimmed, isShareable);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-2" data-testid={`${testIdPrefix}-editor`}>
      <RichTextEditor
        minRows={3}
        value={content}
        onChange={setContent}
        placeholder={placeholder}
        aria-label="Entry content"
        data-testid={`${testIdPrefix}-content`}
      />
      <label htmlFor={checkboxId} className="flex items-center gap-2 text-sm text-brand-slate-600">
        <input
          id={checkboxId}
          type="checkbox"
          checked={isShareable}
          onChange={(e) => setIsShareable(e.target.checked)}
          className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
          data-testid={checkboxId}
        />
        Share this with my team
      </label>
      <div className="flex items-center gap-2">
        <Button
          onClick={() => void handleSubmit()}
          disabled={!trimmed}
          loading={saving}
          data-testid={`${testIdPrefix}-save`}
        >
          {submitLabel}
        </Button>
        <Button variant="ghost" onClick={onCancel} data-testid={`${testIdPrefix}-cancel`}>
          Cancel
        </Button>
      </div>
    </div>
  );
}
