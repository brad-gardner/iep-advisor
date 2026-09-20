import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Editor, EditorContent, useEditor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { Placeholder } from '@tiptap/extension-placeholder';
import { Markdown } from '@tiptap/markdown';
import {
  Bold as BoldIcon,
  Italic as ItalicIcon,
  Strikethrough as StrikethroughIcon,
  Heading2 as Heading2Icon,
  Heading3 as Heading3Icon,
  List as BulletListIcon,
  ListOrdered as OrderedListIcon,
  Quote as QuoteIcon,
  Link2 as LinkIcon,
  Undo2 as UndoIcon,
  Redo2 as RedoIcon,
} from 'lucide-react';
import { cn } from '@/lib/cn';
import { isMarkdownOverLimit, RichTextEditorFallback, type RichTextEditorProps } from './rich-text-editor';
import { MarkdownLimit } from './rich-text-editor-limit';

// Rows-to-height conversion mirrors the plain `<textarea>` sizing model
// (line-height 1.25rem for `text-sm`, `py-2` = 1rem of vertical padding) so a
// caller passing `minRows={3}` gets a writing area roughly the height of a
// 3-row textarea. Duplicated from rich-text-editor.tsx's copy (used to size
// the Suspense fallback) rather than imported, so that module never
// statically pulls this implementation into the eagerly-loaded chunk.
const LINE_HEIGHT_REM = 1.25;
const VERTICAL_PADDING_REM = 1;
function minHeightRem(minRows: number): string {
  return `${minRows * LINE_HEIGHT_REM + VERTICAL_PADDING_REM}rem`;
}

interface ToolbarButtonProps {
  onClick: () => void;
  isActive?: boolean;
  disabled?: boolean;
  title: string;
  children: React.ReactNode;
}

function ToolbarButton({ onClick, isActive, disabled, title, children }: ToolbarButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={title}
      aria-pressed={isActive}
      className={cn(
        'inline-flex h-7 w-7 items-center justify-center rounded text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50',
        isActive
          ? 'bg-brand-teal-50 text-brand-teal-600'
          : 'text-brand-slate-500 hover:bg-brand-slate-100'
      )}
    >
      {children}
    </button>
  );
}

function ToolbarDivider() {
  return <div className="mx-1 h-5 w-px bg-brand-slate-200" aria-hidden="true" />;
}

function LinkControl({ editor, disabled }: { editor: Editor; disabled?: boolean }) {
  const [showInput, setShowInput] = useState(false);
  const [url, setUrl] = useState('');

  const applyLink = useCallback(() => {
    if (!url.trim()) {
      editor.chain().focus().extendMarkRange('link').unsetLink().run();
    } else {
      const href = /^https?:\/\//i.test(url.trim()) ? url.trim() : `https://${url.trim()}`;
      editor.chain().focus().extendMarkRange('link').setLink({ href }).run();
    }
    setShowInput(false);
    setUrl('');
  }, [editor, url]);

  const cancel = useCallback(() => {
    setShowInput(false);
    setUrl('');
    // Closing the URL input swaps it back out for the toolbar button; without
    // explicitly refocusing the editor, focus is left nowhere (the input just
    // unmounted), so Escape silently drops keyboard focus out of the field
    // entirely instead of returning the user to where they were editing.
    editor.chain().focus().run();
  }, [editor]);

  if (showInput) {
    return (
      <div className="flex items-center gap-1">
        <input
          type="url"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              applyLink();
            }
            if (e.key === 'Escape') {
              e.preventDefault();
              cancel();
            }
          }}
          placeholder="https://…"
          aria-label="Link URL"
          autoFocus
          className="h-7 w-40 rounded border border-brand-slate-200 px-2 text-xs text-brand-slate-800 focus:border-brand-teal-400 focus:outline-none focus:ring-[3px] focus:ring-brand-teal-50"
        />
      </div>
    );
  }

  return (
    <ToolbarButton
      onClick={() => {
        setUrl(editor.getAttributes('link').href || '');
        setShowInput(true);
      }}
      isActive={editor.isActive('link')}
      disabled={disabled}
      title="Link"
    >
      <LinkIcon className="h-4 w-4" aria-hidden="true" />
    </ToolbarButton>
  );
}

function Toolbar({ editor, disabled }: { editor: Editor; disabled?: boolean }) {
  return (
    <div
      role="toolbar"
      aria-label="Formatting"
      className="flex flex-wrap items-center gap-0.5 border-b border-brand-slate-200 bg-brand-slate-50 px-2 py-1"
    >
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleBold().run()}
        isActive={editor.isActive('bold')}
        disabled={disabled}
        title="Bold"
      >
        <BoldIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleItalic().run()}
        isActive={editor.isActive('italic')}
        disabled={disabled}
        title="Italic"
      >
        <ItalicIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleStrike().run()}
        isActive={editor.isActive('strike')}
        disabled={disabled}
        title="Strikethrough"
      >
        <StrikethroughIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>

      <ToolbarDivider />

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
        isActive={editor.isActive('heading', { level: 2 })}
        disabled={disabled}
        title="Heading 2"
      >
        <Heading2Icon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}
        isActive={editor.isActive('heading', { level: 3 })}
        disabled={disabled}
        title="Heading 3"
      >
        <Heading3Icon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>

      <ToolbarDivider />

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleBulletList().run()}
        isActive={editor.isActive('bulletList')}
        disabled={disabled}
        title="Bullet list"
      >
        <BulletListIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleOrderedList().run()}
        isActive={editor.isActive('orderedList')}
        disabled={disabled}
        title="Numbered list"
      >
        <OrderedListIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>

      <ToolbarDivider />

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleBlockquote().run()}
        isActive={editor.isActive('blockquote')}
        disabled={disabled}
        title="Quote"
      >
        <QuoteIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>

      <ToolbarDivider />

      <LinkControl editor={editor} disabled={disabled} />

      <ToolbarDivider />

      <ToolbarButton onClick={() => editor.chain().focus().undo().run()} disabled={disabled} title="Undo">
        <UndoIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>
      <ToolbarButton onClick={() => editor.chain().focus().redo().run()} disabled={disabled} title="Redo">
        <RedoIcon className="h-4 w-4" aria-hidden="true" />
      </ToolbarButton>
    </div>
  );
}

/** The production extension set — shared by the mounted editor and `warmEditor`. */
function buildExtensions({ placeholder, maxLength }: { placeholder: string; maxLength?: number }) {
  return [
    StarterKit.configure({
      heading: { levels: [2, 3] },
      link: { openOnClick: false },
      // Markdown has no underline syntax. StarterKit's bundled Underline
      // extension falls back to a non-standard `++text++` marker that this
      // app's `Markdown` display component (react-markdown + remark-gfm)
      // doesn't understand, so it's disabled outright — not just hidden
      // from the toolbar — which also removes its Ctrl/Cmd+U shortcut.
      underline: false,
    }),
    Placeholder.configure({ placeholder }),
    Markdown.configure({ markedOptions: { gfm: true } }),
    ...(maxLength ? [MarkdownLimit.configure({ limit: maxLength })] : []),
  ];
}

function RichTextEditorImpl({
  id,
  label,
  value,
  onChange,
  placeholder,
  disabled,
  maxLength,
  minRows = 3,
  maxHeight,
  onFocus,
  onBlur,
  'data-testid': dataTestId,
  className = '',
  'aria-label': ariaLabelProp,
  required,
  onReady,
}: RichTextEditorProps) {
  // Refs keep the latest callbacks available to TipTap's event handlers
  // without forcing the editor to be recreated when the parent re-renders
  // with new (but behaviorally identical) callback references.
  const onChangeRef = useRef(onChange);
  const onFocusRef = useRef(onFocus);
  const onBlurRef = useRef(onBlur);
  const onReadyRef = useRef(onReady);
  useEffect(() => {
    onChangeRef.current = onChange;
    onFocusRef.current = onFocus;
    onBlurRef.current = onBlur;
    onReadyRef.current = onReady;
  });

  const editorId = id || label?.toLowerCase().replace(/\s+/g, '-');
  const labelId = editorId ? `${editorId}-label` : undefined;
  const counterId = editorId ? `${editorId}-count` : undefined;

  // Tracks the markdown value this editor last emitted (via onChange) or was
  // last synced to (from `value`). Comparing incoming `value` against this
  // ref — rather than a `hasUserEdited` flag — is what lets a parent reset
  // `value` to '' after a successful submit clear the editor, while a
  // debounced save round-trip that hands the same markdown back does not
  // reset the doc/cursor mid-edit.
  const lastValueRef = useRef(value);
  const [markdownForCount, setMarkdownForCount] = useState(value);
  // `content` is read only when the editor is constructed; passing the live
  // `value` would make TipTap's option comparison see a change on every
  // keystroke and run setOptions()/updateState() needlessly. State (not a
  // ref) because it is read during render; it never changes after mount and
  // later external values are applied by the sync effect below.
  const [initialValue] = useState(value);

  // Memoized so `useEditor`'s own option-comparison (it recreates
  // ProseMirror plugins / calls `view.updateState()` whenever the identity
  // of `extensions`/`editorProps` changes) doesn't churn on every parent
  // re-render — only when something that actually changes editor behavior
  // changes.
  const extensions = useMemo(
    () => buildExtensions({ placeholder: placeholder ?? '', maxLength }),
    [placeholder, maxLength]
  );

  const editorProps = useMemo(
    () => ({
      attributes: {
        class: 'px-3 py-2 text-sm text-brand-slate-800 focus:outline-none',
        style: `min-height: ${minHeightRem(minRows)};`,
        ...(editorId ? { id: editorId } : {}),
        ...(dataTestId ? { 'data-testid': dataTestId } : {}),
        ...(ariaLabelProp
          ? { 'aria-label': ariaLabelProp }
          : labelId
            ? { 'aria-labelledby': labelId }
            : {}),
        ...(required ? { 'aria-required': 'true' } : {}),
        role: 'textbox',
        'aria-multiline': 'true',
      },
    }),
    [minRows, editorId, dataTestId, ariaLabelProp, labelId, required]
  );

  const editor = useEditor({
    // Create the editor in an effect after commit rather than during render.
    // With the editor built inside render, every render attempt React
    // discards (an API response landing mid-render, a Suspense retry) leaves
    // an orphaned Editor whose view never attaches — profiled at several
    // createEditor calls per cold open of a busy form. One deferred creation
    // costs a single placeholder frame instead.
    immediatelyRender: false,
    extensions,
    content: initialValue,
    contentType: 'markdown',
    editable: !disabled,
    onUpdate: ({ editor }) => {
      const markdown = editor.isEmpty ? '' : editor.getMarkdown();
      lastValueRef.current = markdown;
      setMarkdownForCount(markdown);
      onChangeRef.current(markdown);
    },
    onFocus: () => onFocusRef.current?.(),
    onBlur: () => onBlurRef.current?.(),
    editorProps,
  });

  // Fire onReady exactly once, when the editor instance is created.
  useEffect(() => {
    if (editor) onReadyRef.current?.(editor);
  }, [editor]);

  // Keep TipTap's own editable state in sync with `disabled` without
  // recreating the editor (which would drop undo history and reset content).
  useEffect(() => {
    editor?.setEditable(!disabled);
  }, [editor, disabled]);

  // External sync: only reset the doc when the parent hands back markdown
  // that differs from what this editor last emitted/was synced to. This
  // covers both the initial load and a parent resetting `value` to '' after
  // a successful submit, while a save round-trip that echoes back the same
  // markdown this editor just emitted is a no-op (no cursor jump).
  useEffect(() => {
    if (!editor || value === lastValueRef.current) return;
    lastValueRef.current = value;
    editor.commands.setContent(value, { contentType: 'markdown', emitUpdate: false });
    setMarkdownForCount(value);
  }, [editor, value]);

  const overLimit = isMarkdownOverLimit(markdownForCount, maxLength);

  // Imperative rather than via `editorProps.attributes` (which is only read
  // when the editor is constructed): `overLimit` changes on nearly every
  // keystroke, and rebuilding editorProps that often would churn `useEditor`
  // far more than the memoized object above already avoids. With the editor
  // created after commit (`immediatelyRender: false`) its view is mounted by
  // `EditorContent` before this effect runs, so `editor.view.dom` is safe.
  useEffect(() => {
    if (!editor || editor.isDestroyed) return;
    const dom = editor.view.dom;
    if (overLimit) {
      dom.setAttribute('aria-invalid', 'true');
      if (counterId) dom.setAttribute('aria-describedby', counterId);
    } else {
      dom.removeAttribute('aria-invalid');
      if (counterId) dom.removeAttribute('aria-describedby');
    }
  }, [editor, overLimit, counterId]);

  if (!editor) {
    return <RichTextEditorFallback label={label} required={required} minRows={minRows} className={className} />;
  }

  return (
    <div className={className}>
      {label && (
        <label
          htmlFor={editorId}
          id={labelId}
          // A `role="textbox"` contenteditable div isn't a "labelable"
          // element (input/button/select/textarea/…), so the browser's
          // native `<label for>` click-to-focus never fires for it. Forward
          // the click to the editor explicitly so clicking the label still
          // moves focus/caret into the field, same as it did for the
          // `<Textarea>` this replaced.
          onClick={() => editor.commands.focus()}
          className="block text-[13px] font-medium text-brand-slate-600 mb-1"
        >
          {label}
          {required && (
            <>
              <span className="ml-1 text-brand-danger-700" aria-hidden="true">
                *
              </span>
              <span className="sr-only"> (required)</span>
            </>
          )}
        </label>
      )}
      <div
        className={cn(
          'overflow-hidden rounded-input border border-brand-slate-200 bg-white transition-colors',
          'focus-within:border-brand-teal-400 focus-within:ring-[3px] focus-within:ring-brand-teal-50',
          disabled && 'bg-brand-slate-50 opacity-70'
        )}
      >
        <Toolbar editor={editor} disabled={disabled} />
        <div
          style={maxHeight ? { maxHeight, overflowY: 'auto' } : undefined}
          className={cn(maxHeight && 'overscroll-contain')}
        >
          <EditorContent editor={editor} />
        </div>
      </div>
      {maxLength && (
        <p
          id={counterId}
          className={cn(
            'mt-1 text-right text-xs',
            overLimit ? 'text-brand-danger-600' : 'text-brand-slate-500'
          )}
        >
          {markdownForCount.length} / {maxLength}
        </p>
      )}
    </div>
  );
}

/**
 * Create and immediately destroy one headless editor with the production
 * extension set so the first real mount finds the code paths warm. See
 * `warmRichTextEditor` in rich-text-editor.tsx.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function warmEditor(): void {
  // A detached element so the view/DOM mount path is exercised too, not just
  // schema and extension setup.
  const editor = new Editor({
    element: document.createElement('div'),
    extensions: buildExtensions({ placeholder: '' }),
    content: '**warm** _up_\n\n- list',
    contentType: 'markdown',
  });
  editor.getMarkdown();
  editor.destroy();
}

export default RichTextEditorImpl;
