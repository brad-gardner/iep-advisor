import ReactMarkdown, { type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import { cn } from '@/lib/cn';

// Renders links opened in a new tab (except in-page `#anchor` links) without
// relying on rehype-raw — raw HTML in the source markdown is never rendered,
// only the tag names react-markdown/rehype-sanitize already understand as
// markdown-produced elements.
const components: Components = {
  a: ({ href, children, ...props }) => {
    const isHashLink = href?.startsWith('#');
    return (
      <a href={href} {...(!isHashLink ? { target: '_blank', rel: 'noopener noreferrer' } : {})} {...props}>
        {children}
      </a>
    );
  },
};

interface MarkdownProps {
  content: string;
  className?: string;
  'data-testid'?: string;
}

/**
 * Displays markdown stored by `RichTextEditor` as sanitized, styled HTML
 * (`.prose-iep` in src/index.css mirrors the editor's own `.ProseMirror`
 * rules, so what a user types looks like what renders here). No rehype-raw:
 * raw HTML embedded in stored markdown is never rendered.
 */
export function Markdown({ content, className, 'data-testid': dataTestId }: MarkdownProps) {
  if (!content || !content.trim()) return null;

  return (
    <div className={cn('prose-iep', className)} data-testid={dataTestId}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]} components={components}>
        {content}
      </ReactMarkdown>
    </div>
  );
}
