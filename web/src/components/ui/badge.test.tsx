import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Badge } from './badge';

describe('Badge', () => {
  it('renders success with the teal treatment', () => {
    render(<Badge variant="success">Active</Badge>);
    expect(screen.getByText('Active')).toHaveClass(
      'bg-brand-teal-50',
      'text-brand-teal-600',
      'border-brand-teal-100'
    );
  });

  it('renders info with a distinct slate treatment (info !== success)', () => {
    const { rerender } = render(<Badge variant="info">Info</Badge>);
    const info = screen.getByText('Info');
    const infoClass = info.className;
    expect(info).toHaveClass('bg-brand-slate-100', 'text-brand-slate-700');

    rerender(<Badge variant="success">Info</Badge>);
    // Same node, different class string — the two variants must not collide.
    expect(screen.getByText('Info').className).not.toEqual(infoClass);
  });

  it('renders error on the brand-danger scale (not raw red)', () => {
    render(<Badge variant="error">Error</Badge>);
    const el = screen.getByText('Error');
    expect(el).toHaveClass('bg-brand-danger-50', 'text-brand-danger-700', 'border-brand-danger-200');
    expect(el.className).not.toContain('bg-red-50');
    expect(el.className).not.toContain('brand-red');
  });

  it('renders neutral distinctly from info', () => {
    const { rerender } = render(<Badge variant="neutral">X</Badge>);
    const neutral = screen.getByText('X').className;
    rerender(<Badge variant="info">X</Badge>);
    expect(screen.getByText('X').className).not.toEqual(neutral);
  });

  it('forwards data-testid via rest props', () => {
    render(
      <Badge variant="neutral" data-testid="my-badge">
        Tag
      </Badge>
    );
    expect(screen.getByTestId('my-badge')).toBeInTheDocument();
  });
});
