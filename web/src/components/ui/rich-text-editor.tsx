import { lazy, Suspense, useState, type ComponentType } from 'react';
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
const loadImpl = () => import('./rich-text-editor-impl');
const RichTextEditorImpl = lazy(loadImpl);
// Once the chunk is in hand (from the idle warm-up or an earlier mount) render
// it directly: `React.lazy` would otherwise still suspend for a tick on its
// first render, which costs a fallback frame plus a re-render of the host form.
let LoadedImpl: ComponentType<RichTextEditorProps> | null = null;

/** Test seam: whether the warm-up has populated the direct-render path. */
// eslint-disable-next-line react-refresh/only-export-components
export function isRichTextEditorWarm(): boolean {
  return LoadedImpl !== null;
}

let warmed = false;
/**
 * Pay the editor's one-time cost while the app is idle instead of on the
 * first field a person opens: fetch + evaluate the TipTap chunk and run one
 * throwaway editor through creation so the ProseMirror/TipTap code paths are
 * already JIT-warm. Measured on a cold session the first mount otherwise
 * carries the whole stack's first execution (several times the warm cost).
 * Safe to call repeatedly; only signed-in surfaces should call it, since it
 * downloads ~150 kB gzip that public pages never need.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function warmRichTextEditor(): void {
  if (warmed) return;
  warmed = true;
  const run = () => {
    void loadImpl()
      .then((m) => {
        LoadedImpl = m.default;
        m.warmEditor();
      })
      .catch(() => {
        warmed = false; // let a later call retry after a transient chunk-load failure
      });
  };
  if (typeof window === 'undefined') return;
  // Safari has no requestIdleCallback; a short timer is the usual stand-in.
  const idle = (window as Window & { requestIdleCallback?: typeof requestIdleCallback }).requestIdleCallback;
  if (idle) idle(run, { timeout: 5000 });
  else window.setTimeout(run, 2000);
}

export function RichTextEditorFallback({
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
  // Decide the render path ONCE per instance. Re-reading `LoadedImpl` on
  // every render would swap element type (Suspense → direct) the moment the
  // warm-up lands, which remounts a field someone is typing in — losing
  // focus, caret and undo history. An instance that mounted through Suspense
  // stays on that path for its lifetime; only later mounts go direct.
  const [Impl] = useState(() => LoadedImpl);
  if (Impl) return <Impl {...props} />;
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
