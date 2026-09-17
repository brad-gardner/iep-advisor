import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Markdown } from './markdown';

describe('Markdown', () => {
  it('renders bold text', () => {
    render(<Markdown content="This is **bold** text." />);
    expect(screen.getByText('bold').tagName).toBe('STRONG');
  });

  it('renders bullet and numbered lists', () => {
    render(<Markdown content={'- one\n- two\n\n1. first\n2. second'} />);
    expect(screen.getByText('one').closest('ul')).not.toBeNull();
    expect(screen.getByText('first').closest('ol')).not.toBeNull();
  });

  it('renders links with target=_blank and rel=noopener noreferrer', () => {
    render(<Markdown content="[a resource](https://example.com/doc)" />);
    const link = screen.getByRole('link', { name: 'a resource' });
    expect(link).toHaveAttribute('href', 'https://example.com/doc');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('does not open a new tab for in-page hash links', () => {
    render(<Markdown content="[jump](#section)" />);
    const link = screen.getByRole('link', { name: 'jump' });
    expect(link).not.toHaveAttribute('target');
  });

  it('strips raw HTML and script tags rather than rendering them', () => {
    const { container } = render(
      <Markdown content={'Before\n\n<script>window.hacked = true;</script>\n\n<div class="raw">After</div>'} />
    );
    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('div.raw')).toBeNull();
    expect((window as unknown as { hacked?: boolean }).hacked).toBeUndefined();
  });

  it('returns null for empty content', () => {
    const { container } = render(<Markdown content="" />);
    expect(container).toBeEmptyDOMElement();
  });

  it('returns null for whitespace-only content', () => {
    const { container } = render(<Markdown content={'   \n  '} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('forwards data-testid and className', () => {
    render(<Markdown content="hi" className="extra" data-testid="note-markdown" />);
    const el = screen.getByTestId('note-markdown');
    expect(el).toHaveClass('prose-iep', 'extra');
  });
});
