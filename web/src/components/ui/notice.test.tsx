import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Notice } from './notice';

// The Notice colours live on the outer container; grab it from the title node.
function containerOf(title: string): HTMLElement {
  const el = screen.getByText(title).closest('div.border');
  if (!el) throw new Error('Notice container not found');
  return el as HTMLElement;
}

describe('Notice', () => {
  it('renders the title text', () => {
    render(<Notice variant="info" title="Heads up" />);
    expect(screen.getByText('Heads up')).toBeInTheDocument();
  });

  it('renders info with a neutral slate treatment', () => {
    render(<Notice variant="info" title="Info notice" />);
    expect(containerOf('Info notice')).toHaveClass('bg-brand-slate-50', 'border-brand-slate-200');
  });

  it('renders success with the teal treatment, distinct from info', () => {
    const { rerender } = render(<Notice variant="info" title="Same title" />);
    const infoClass = containerOf('Same title').className;

    rerender(<Notice variant="success" title="Same title" />);
    const successContainer = containerOf('Same title');
    expect(successContainer).toHaveClass('bg-brand-teal-50', 'border-brand-teal-100');
    expect(successContainer.className).not.toEqual(infoClass);
  });

  it('renders error on the brand-danger scale (not raw red)', () => {
    render(<Notice variant="error" title="Error notice" />);
    const container = containerOf('Error notice');
    expect(container).toHaveClass('bg-brand-danger-50', 'border-brand-danger-200');
    expect(container.className).not.toContain('bg-red-50');
    expect(container.className).not.toContain('border-red-200');
  });
});
