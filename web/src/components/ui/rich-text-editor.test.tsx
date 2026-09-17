import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import type { Editor } from '@tiptap/react';
import { RichTextEditor, isMarkdownOverLimit } from './rich-text-editor';

// This suite exercises the real TipTap editor (jsdom supports the
// contenteditable/ProseMirror machinery needed to drive commands and read
// back markdown). Every other test in the app gets the plain-textarea stand-
// in registered globally in src/test/setup.ts.
//
// The public `RichTextEditor` lazy-loads its TipTap-backed implementation
// (rich-text-editor-impl.tsx) behind `React.lazy`/`Suspense`, so every test
// below awaits the loaded editor (`findBy…`, `waitFor`, or the `ready`
// promise from `readyCapture`) rather than querying synchronously right
// after `render`.
vi.unmock('@/components/ui/rich-text-editor');

function readyCapture() {
  let editor: Editor | undefined;
  let resolveReady!: (e: Editor) => void;
  const ready = new Promise<Editor>((resolve) => {
    resolveReady = resolve;
  });
  return {
    onReady: (e: Editor) => {
      editor = e;
      resolveReady(e);
    },
    ready,
    get current() {
      if (!editor) throw new Error('editor not ready');
      return editor;
    },
  };
}

describe('RichTextEditor', () => {
  it('renders initial markdown as rich content (bold, list)', async () => {
    render(<RichTextEditor label="Note" value={'**bold**\n\n- item one'} onChange={() => {}} />);
    const region = await screen.findByLabelText('Note');
    expect(within(region).getByText('bold').tagName).toBe('STRONG');
    expect(within(region).getByText('item one').closest('li')).not.toBeNull();
  });

  it('emits markdown for bold text produced via editor commands', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="" onChange={handleChange} onReady={ready.onReady} />);
    await ready.ready;

    act(() => {
      ready.current.chain().focus().toggleBold().insertContent('bold text').run();
    });

    expect(handleChange).toHaveBeenCalled();
    const lastMarkdown = handleChange.mock.calls.at(-1)?.[0];
    expect(lastMarkdown).toContain('**bold text**');
  });

  it('emits markdown for a bullet list produced via editor commands', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="" onChange={handleChange} onReady={ready.onReady} />);
    await ready.ready;

    act(() => {
      ready.current.chain().focus().toggleBulletList().insertContent('item').run();
    });

    expect(handleChange).toHaveBeenCalled();
    const lastMarkdown = handleChange.mock.calls.at(-1)?.[0];
    expect(lastMarkdown.trim()).toMatch(/^-\s+item$/);
  });

  it('emits an empty string once the doc is cleared back to empty', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="hello" onChange={handleChange} onReady={ready.onReady} />);
    await ready.ready;

    act(() => {
      ready.current.commands.clearContent(true);
    });

    expect(handleChange).toHaveBeenLastCalledWith('');
  });

  it('clears the editor when the parent resets value to empty string', async () => {
    const ready = readyCapture();
    const { rerender } = render(
      <RichTextEditor label="Note" value="hello world" onChange={() => {}} onReady={ready.onReady} />
    );
    await ready.ready;
    expect(await screen.findByText('hello world')).toBeInTheDocument();

    rerender(<RichTextEditor label="Note" value="" onChange={() => {}} onReady={ready.onReady} />);

    await waitFor(() => expect(screen.queryByText('hello world')).not.toBeInTheDocument());
    expect(ready.current.isEmpty).toBe(true);
  });

  it('does not reset the doc when the parent echoes back the last-emitted markdown', async () => {
    const ready = readyCapture();
    function Controlled() {
      const [value, setValue] = useState('');
      return <RichTextEditor label="Note" value={value} onChange={setValue} onReady={ready.onReady} />;
    }
    render(<Controlled />);
    await ready.ready;

    act(() => {
      ready.current.chain().focus().insertContent('typed').run();
    });

    // The onChange → setState → re-render round trip hands the same markdown
    // back as `value`; the editor must not reset (no cursor jump / doc replace).
    expect(screen.getByText('typed')).toBeInTheDocument();
  });

  it('is non-editable when disabled', async () => {
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="hello" onChange={() => {}} disabled onReady={ready.onReady} />
    );
    await ready.ready;

    expect(ready.current.isEditable).toBe(false);
    expect(await screen.findByLabelText('Note')).toHaveAttribute('contenteditable', 'false');
  });

  it('disables toolbar buttons when disabled', async () => {
    render(<RichTextEditor label="Note" value="" onChange={() => {}} disabled />);

    expect(await screen.findByRole('button', { name: 'Bold' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Link' })).toBeDisabled();
  });

  it('does not render an Underline toolbar button — markdown has no underline syntax', async () => {
    render(<RichTextEditor label="Note" value="" onChange={() => {}} />);
    await screen.findByRole('toolbar');
    expect(screen.queryByRole('button', { name: 'Underline' })).not.toBeInTheDocument();
  });

  it('does not register the underline mark at all, so its Ctrl/Cmd+U shortcut is gone too', async () => {
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="" onChange={() => {}} onReady={ready.onReady} />);
    await ready.ready;

    expect(ready.current.extensionManager.extensions.some((ext) => ext.name === 'underline')).toBe(false);
  });

  it('focuses the editor when the label is clicked — a role="textbox" div is not a labelable element', async () => {
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="" onChange={() => {}} onReady={ready.onReady} />);
    await ready.ready;

    const label = await screen.findByText('Note');
    expect(ready.current.isFocused).toBe(false);
    fireEvent.click(label);

    await waitFor(() => expect(ready.current.isFocused).toBe(true));
  });
});

describe('RichTextEditor markdown length limit (MarkdownLimit)', () => {
  it('blocks typing that would push the serialized markdown past maxLength', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="abcde" onChange={handleChange} maxLength={5} onReady={ready.onReady} />
    );
    await ready.ready;

    act(() => {
      ready.current.chain().focus('end').insertContent('f').run();
    });

    expect(ready.current.getMarkdown()).toBe('abcde');
    expect(handleChange).not.toHaveBeenCalledWith('abcdef');
  });

  it('blocks a formatting toggle at the cap when it would lengthen the markdown', async () => {
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="abcde" onChange={() => {}} maxLength={5} onReady={ready.onReady} />);
    await ready.ready;

    act(() => {
      ready.current.chain().focus().selectAll().toggleBold().run();
    });

    // "abcde" (5) -> "**abcde**" (9) would be both over the limit and longer
    // than before, so the whole transaction is rejected.
    expect(ready.current.getMarkdown()).toBe('abcde');
  });

  it('always allows a deletion (e.g. backspace), even while already over the limit', async () => {
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="abcdefghij" onChange={() => {}} maxLength={5} onReady={ready.onReady} />
    );
    await ready.ready;
    expect(ready.current.getMarkdown().length).toBeGreaterThan(5);

    act(() => {
      ready.current.chain().focus().deleteRange({ from: 1, to: 2 }).run();
    });

    expect(ready.current.getMarkdown()).toBe('bcdefghij');
  });

  it('rejects an insert that would overflow the limit in one transaction (e.g. a paste)', async () => {
    const ready = readyCapture();
    render(<RichTextEditor label="Note" value="abc" onChange={() => {}} maxLength={5} onReady={ready.onReady} />);
    await ready.ready;

    act(() => {
      ready.current.chain().focus('end').insertContent('this is way too much text to fit').run();
    });

    expect(ready.current.getMarkdown()).toBe('abc');
  });

  it('shows an over-limit counter and marks the editor invalid for an externally-supplied over-limit value', async () => {
    const value = 'this value is over the configured limit';
    render(<RichTextEditor label="Note" value={value} onChange={() => {}} maxLength={10} />);

    expect(await screen.findByText(`${value.length} / 10`)).toHaveClass('text-brand-danger-600');
    expect(await screen.findByLabelText('Note')).toHaveAttribute('aria-invalid', 'true');
  });

  it('does not show over-limit styling under the cap', async () => {
    render(<RichTextEditor label="Note" value="short" onChange={() => {}} maxLength={10} />);

    expect(await screen.findByText('5 / 10')).toHaveClass('text-brand-slate-400');
    expect(await screen.findByLabelText('Note')).not.toHaveAttribute('aria-invalid');
  });
});

describe('RichTextEditor LinkControl', () => {
  async function openLinkInput() {
    const linkButton = await screen.findByRole('button', { name: 'Link' });
    fireEvent.click(linkButton);
    return screen.findByLabelText('Link URL');
  }

  it('applies a bare domain with an https:// prefix', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="reading fluency" onChange={handleChange} onReady={ready.onReady} />
    );
    await ready.ready;
    act(() => {
      ready.current.commands.selectAll();
    });

    const input = await openLinkInput();
    fireEvent.change(input, { target: { value: 'example.com' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => {
      expect(handleChange.mock.calls.at(-1)?.[0]).toContain('[reading fluency](https://example.com)');
    });
  });

  it('applies an already-prefixed http(s) URL unchanged', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="reading fluency" onChange={handleChange} onReady={ready.onReady} />
    );
    await ready.ready;
    act(() => {
      ready.current.commands.selectAll();
    });

    const input = await openLinkInput();
    fireEvent.change(input, { target: { value: 'https://example.com/path' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => {
      expect(handleChange.mock.calls.at(-1)?.[0]).toContain('[reading fluency](https://example.com/path)');
    });
  });

  it('unsets the link when the URL is submitted empty', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(
      <RichTextEditor
        label="Note"
        value="[reading fluency](https://example.com)"
        onChange={handleChange}
        onReady={ready.onReady}
      />
    );
    await ready.ready;
    act(() => {
      ready.current.commands.setTextSelection(3);
    });

    const linkButton = await screen.findByRole('button', { name: 'Link' });
    fireEvent.click(linkButton);
    const input = await screen.findByLabelText('Link URL');
    expect(input).toHaveValue('https://example.com');

    fireEvent.change(input, { target: { value: '' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => {
      expect(handleChange.mock.calls.at(-1)?.[0]).not.toContain('](');
    });
  });

  it('cancels on Escape without applying, and restores focus to the editor', async () => {
    const handleChange = vi.fn();
    const ready = readyCapture();
    render(
      <RichTextEditor label="Note" value="reading fluency" onChange={handleChange} onReady={ready.onReady} />
    );
    await ready.ready;
    act(() => {
      ready.current.commands.selectAll();
    });

    const input = await openLinkInput();
    fireEvent.change(input, { target: { value: 'example.com' } });
    fireEvent.keyDown(input, { key: 'Escape' });

    await waitFor(() => expect(screen.queryByLabelText('Link URL')).not.toBeInTheDocument());
    expect(handleChange.mock.calls.at(-1)?.[0] ?? '').not.toContain('example.com');
    // Closing the input swaps it back out for the toolbar button; without an
    // explicit refocus, focus would be dropped out of the field entirely.
    await waitFor(() => expect(ready.current.isFocused).toBe(true));
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
