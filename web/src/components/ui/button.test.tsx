import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Button } from './button';

describe('Button', () => {
  it('defaults to the md size with the historical padding/text classes', () => {
    render(<Button>Save</Button>);
    const btn = screen.getByRole('button', { name: 'Save' });
    expect(btn).toHaveClass('px-4', 'py-2', 'text-[13px]');
    // primary variant classes preserved
    expect(btn).toHaveClass('bg-brand-teal-500');
  });

  it('is not disabled and has no aria-busy when not loading', () => {
    render(<Button>Save</Button>);
    const btn = screen.getByRole('button', { name: 'Save' });
    expect(btn).not.toBeDisabled();
    expect(btn).not.toHaveAttribute('aria-busy');
  });

  it('renders children directly (no wrapper) when not loading', () => {
    render(
      <Button>
        <span data-testid="icon" />
        Save
      </Button>
    );
    const btn = screen.getByRole('button');
    // The icon is a direct child of the button, not nested in a label wrapper.
    expect(screen.getByTestId('icon').parentElement).toBe(btn);
  });

  it('applies sm and lg size classes', () => {
    const { rerender } = render(<Button size="sm">Save</Button>);
    expect(screen.getByRole('button')).toHaveClass('px-3', 'py-1.5', 'text-xs');
    rerender(<Button size="lg">Save</Button>);
    expect(screen.getByRole('button')).toHaveClass('px-5', 'py-2.5', 'text-sm');
  });

  it('when loading: disables, sets aria-busy, and shows a spinner while keeping the label', () => {
    render(<Button loading>Save</Button>);
    const btn = screen.getByRole('button', { name: 'Save' });
    expect(btn).toBeDisabled();
    expect(btn).toHaveAttribute('aria-busy', 'true');
    // The accessible name is preserved (label kept in the DOM).
    expect(btn).toHaveTextContent('Save');
    // A spinner is present (role status), overlaid.
    expect(btn.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('loading disables even without an explicit disabled prop and cannot be re-enabled', () => {
    render(
      <Button loading disabled={false}>
        Save
      </Button>
    );
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('danger variant uses the brand-danger scale (not raw red)', () => {
    render(<Button variant="danger">Delete</Button>);
    const btn = screen.getByRole('button', { name: 'Delete' });
    expect(btn).toHaveClass('text-brand-danger-700', 'hover:bg-brand-danger-50');
    expect(btn.className).not.toContain('brand-red');
    expect(btn.className).not.toContain('bg-red-50');
  });
});
