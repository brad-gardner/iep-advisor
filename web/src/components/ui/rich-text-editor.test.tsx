import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import type { Editor } from '@tiptap/react';
import { RichTextEditor, isMarkdownOverLimit } from './rich-text-editor';

// This suite exercises the real TipTap editor (jsdom supports the
// contenteditable/ProseMirror machinery needed to drive commands and read
// back markdown). Every other test in the app gets the plain-textarea stand-
// in registered globally in src/test/setup.ts.
vi.unmock('@/components/ui/rich-text-editor');

function readyCapture() {
  let editor: Editor | undefined;
  return {
    onReady: (e: Editor) => {
      editor = e;
    },
    get current() {
      if (!editor) throw new Error('editor not ready');
      return editor;
    },
  };
}

describe('RichTextEditor', () => {
  it('renders initial markdown as rich content (bold, list)', () => {
    render(
      <RichTextEditor label="Note" value={'**bold**\n\n- item one'} onChange={() => {}} />
    );
    const region = screen.getByLabelText('Note');
    expect(within(region).getByText('bold').tagName).toBe('STRONG');
    expect(within(region).getByText('item one').closest('li')).not.toBeNull();
  });

  it('emits markdown for bold text produced via editor commands', () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="" onChange={handleChange} onReady={ready.onReady} />);

    act(() => {
      ready.current.chain().focus().toggleBold().insertContent('bold text').run();
    });

    expect(handleChange).toHaveBeenCalled();
    const lastMarkdown = handleChange.mock.calls.at(-1)?.[0];
    expect(lastMarkdown).toContain('**bold text**');
  });

  it('emits markdown for a bullet list produced via editor commands', () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="" onChange={handleChange} onReady={ready.onReady} />);

    act(() => {
      ready.current.chain().focus().toggleBulletList().insertContent('item').run();
    });

    expect(handleChange).toHaveBeenCalled();
    const lastMarkdown = handleChange.mock.calls.at(-1)?.[0];
    expect(lastMarkdown.trim()).toMatch(/^-\s+item$/);
  });

  it('emits an empty string once the doc is cleared back to empty', () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="hello" onChange={handleChange} onReady={ready.onReady} />);

    act(() => {
      ready.current.commands.clearContent(true);
    });

    expect(handleChange).toHaveBeenLastCalledWith('');
  });

  it('clears the editor when the parent resets value to empty string', () => {
    const ready = readyCapture();
    const { rerender } = render(
      <RichTextEditor label="Note" value="hello world" onChange={() => {}} onReady={ready.onReady} />
    );
    expect(screen.getByText('hello world')).toBeInTheDocument();

    rerender(<RichTextEditor label="Note" value="" onChange={() => {}} onReady={ready.onReady} />);

    expect(screen.queryByText('hello world')).not.toBeInTheDocument();
    expect(ready.current.isEmpty).toBe(true);
  });

  it('does not reset the doc when the parent echoes back the last-emitted markdown', () => {
    const ready = readyCapture();
    function Controlled() {
      const [value, setValue] = useState('');
      return <RichTextEditor label="Note" value={value} onChange={setValue} onReady={ready.onReady} />;
    }
    render(<Controlled />);

    act(() => {
      ready.current.chain().focus().insertContent('typed').run();
    });

    // The onChange → setState → re-render round trip hands the same markdown
    // back as `value`; the editor must not reset (no cursor jump / doc replace).
    expect(screen.getByText('typed')).toBeInTheDocument();
  });

  it('shows an over-limit counter and marks the editor invalid', () => {
    const value = 'this value is over the configured limit';
    render(<RichTextEditor label="Note" value={value} onChange={() => {}} maxLength={10} />);

    expect(screen.getByText(`${value.length} / 10`)).toHaveClass('text-brand-danger-600');
    expect(screen.getByLabelText('Note')).toHaveAttribute('aria-invalid', 'true');
  });

  it('does not show over-limit styling under the cap', () => {
    render(<RichTextEditor label="Note" value="short" onChange={() => {}} maxLength={10} />);

    expect(screen.getByText('5 / 10')).toHaveClass('text-brand-slate-400');
    expect(screen.getByLabelText('Note')).not.toHaveAttribute('aria-invalid');
  });

  it('is non-editable when disabled', () => {
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="hello" onChange={() => {}} disabled onReady={ready.onReady} />
    );

    expect(ready.current.isEditable).toBe(false);
    expect(screen.getByLabelText('Note')).toHaveAttribute('contenteditable', 'false');
  });

  it('disables toolbar buttons when disabled', () => {
    render(<RichTextEditor label="Note" value="" onChange={() => {}} disabled />);

    expect(screen.getByRole('button', { name: 'Bold' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Link' })).toBeDisabled();
  });
});

describe('isMarkdownOverLimit', () => {
  it('is false when no maxLength is given', () => {
    expect(isMarkdownOverLimit('anything at all', undefined)).toBe(false);
  });

  it('is true once markdown length exceeds maxLength, false at/under it', () => {
    expect(isMarkdownOverLimit('12345', 4)).toBe(true);
    expect(isMarkdownOverLimit('1234', 4)).toBe(false);
  });
});
