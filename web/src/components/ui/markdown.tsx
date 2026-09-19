import { memo } from 'react';
import ReactMarkdown, { type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import { cn } from '@/lib/cn';

// Renders links opened in a new tab (except in-page `#anchor` links) without
// relying on rehype-raw — raw HTML in the source markdown is never rendered,
// only the tag names react-markdown/rehype-sanitize already understand as
// markdown-produced elements.
// GFM tables get their own horizontal scroll region so a wide table (the
// advocate likes comparing IEP versions in one) never widens the page on a
// phone; the styles live under `.prose-iep` in src/index.css.
// `node` is react-markdown's hast element — not a DOM attribute, so it stays off the `<table>`.
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const table: Components['table'] = ({ children, node, ...props }) => (
  <div className="prose-iep-table-wrap">
    <table {...props}>{children}</table>
  </div>
);

const components: Components = {
  a: ({ href, children, ...props }) => {
    const isHashLink = href?.startsWith('#');
    return (
      <a href={href} {...(!isHashLink ? { target: '_blank', rel: 'noopener noreferrer' } : {})} {...props}>
        {children}
      </a>
    );
  },
  table,
};

// Same renderer, but with links downgraded to plain, non-interactive spans —
// for markdown displayed inside an already-interactive element (e.g. a
// `<button role="option">` row), where a nested `<a>` would put an
// interactive element inside another one.
const componentsNoLinks: Components = {
  a: ({ children }) => <span>{children}</span>,
  table,
};

interface MarkdownProps {
  content: string;
  className?: string;
  'data-testid'?: string;
  /** Render links as plain text instead of `<a>` — for markdown displayed
   *  inside another interactive element (button, option row, …). */
  disableLinks?: boolean;
}

/**
 * Displays markdown stored by `RichTextEditor` as sanitized, styled HTML
 * (`.prose-iep` in src/index.css mirrors the editor's own `.ProseMirror`
 * rules, so what a user types looks like what renders here). No rehype-raw:
 * raw HTML embedded in stored markdown is never rendered.
 *
 * Memoized: react-markdown builds a fresh unified processor and reparses
 * `content` on every render with no memoization of its own, so this bails
 * out on unrelated parent re-renders instead of re-parsing markdown that
 * hasn't changed — this matters most in lists (goal history, contributions,
 * responses) that render one `<Markdown>` per item.
 */
export const Markdown = memo(function Markdown({
  content,
  className,
  'data-testid': dataTestId,
  disableLinks,
}: MarkdownProps) {
  if (!content || !content.trim()) return null;

  return (
    <div className={cn('prose-iep', className)} data-testid={dataTestId}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={disableLinks ? componentsNoLinks : components}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
});
