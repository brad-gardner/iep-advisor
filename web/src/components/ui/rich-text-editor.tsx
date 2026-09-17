import { lazy, Suspense } from 'react';
import type { Editor } from '@tiptap/react';

/**
 * Returns true when markdown text exceeds maxLength. Shared with forms that
 * need to gate submission on the same rule the editor uses for its own
 * over-limit counter/aria-invalid state (see `MarkdownLimit` in
 * rich-text-editor-limit.ts, which enforces the same rule against typing).
 *
 * Deliberately dependency-free — no TipTap/ProseMirror import — so that
 * forms which only need this pure length check never pull in the
 * lazy-loaded editor implementation just by importing this module.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function isMarkdownOverLimit(markdown: string, maxLength?: number): boolean {
  if (!maxLength) return false;
  return markdown.length > maxLength;
}

// Rows-to-height conversion mirrors the plain `<textarea>` sizing model
// (line-height 1.25rem for `text-sm`, `py-2` = 1rem of vertical padding), and
// is duplicated (not imported) from rich-text-editor-impl.tsx's copy of the
// same math so that this file — the public, eagerly-loaded entry point —
// never statically imports the TipTap-backed implementation. It sizes the
// Suspense fallback below to match the loaded editor's footprint so layout
// doesn't jump once the lazy chunk resolves.
const LINE_HEIGHT_REM = 1.25;
const VERTICAL_PADDING_REM = 1;
const TOOLBAR_HEIGHT_REM = 2.25; // h-7 (1.75rem) buttons + py-1 (0.5rem) + border
function minHeightRem(minRows: number): string {
  return `${minRows * LINE_HEIGHT_REM + VERTICAL_PADDING_REM}rem`;
}

export interface RichTextEditorProps {
  id?: string;
  label?: string;
  /** Editor content as Markdown. */
  value: string;
  /** Called with the editor content as Markdown on every edit. */
  onChange: (markdown: string) => void;
  placeholder?: string;
  disabled?: boolean;
  maxLength?: number;
  /** Minimum visible rows for the writing area (default 3). */
  minRows?: number;
  /** CSS max-height for the writing area; the toolbar stays pinned above it. */
  maxHeight?: string;
  onFocus?: () => void;
  onBlur?: () => void;
  'data-testid'?: string;
  className?: string;
  'aria-label'?: string;
  required?: boolean;
  /** Called once the TipTap editor instance is created — used by tests to drive commands. */
  onReady?: (editor: Editor) => void;
}

// The TipTap/ProseMirror implementation is the dominant share of this
// editor's weight (~200KB gzip: @tiptap/react, @tiptap/starter-kit,
// @tiptap/markdown, @tiptap/extensions, plus the prosemirror-* packages they
// pull in). Code-splitting it behind `React.lazy` means pages that only ever
// display stored markdown (via `Markdown`) — parent/student read-only
// views, login, the district admin screens — never fetch any of it.
const RichTextEditorImpl = lazy(() => import('./rich-text-editor-impl'));

function RichTextEditorFallback({
  label,
  required,
  minRows = 3,
  className = '',
}: Pick<RichTextEditorProps, 'label' | 'required' | 'minRows' | 'className'>) {
  return (
    <div className={className}>
      {label && (
        <span className="block text-[13px] font-medium text-brand-slate-600 mb-1">
          {label}
          {required && (
            <span className="ml-1 text-brand-danger-700" aria-hidden="true">
              *
            </span>
          )}
        </span>
      )}
      <div
        aria-busy="true"
        className="rounded-input border border-brand-slate-200 bg-white"
        style={{ minHeight: `calc(${TOOLBAR_HEIGHT_REM}rem + ${minHeightRem(minRows)})` }}
      />
    </div>
  );
}

/**
 * Public entry point for the shared rich text (markdown) editor. Keeps the
 * stable API (`RichTextEditor`, `RichTextEditorProps`, `isMarkdownOverLimit`)
 * while the real TipTap-backed component loads lazily — see
 * rich-text-editor-impl.tsx for the implementation.
 */
export function RichTextEditor(props: RichTextEditorProps) {
  return (
    <Suspense
      fallback={
        <RichTextEditorFallback label={props.label} required={props.required} minRows={props.minRows} className={props.className} />
      }
    >
      <RichTextEditorImpl {...props} />
    </Suspense>
  );
}
