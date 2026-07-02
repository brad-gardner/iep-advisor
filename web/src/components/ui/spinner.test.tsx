import { describe, it, expect } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { Spinner } from './spinner';

describe('Spinner', () => {
  it('renders a status role for assistive tech', () => {
    render(<Spinner />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('exposes the default label inside the status region', () => {
    render(<Spinner />);
    expect(within(screen.getByRole('status')).getByText('Loading…')).toBeInTheDocument();
  });

  it('exposes a custom label inside the status region', () => {
    render(<Spinner label="Loading staff" />);
    expect(within(screen.getByRole('status')).getByText('Loading staff')).toBeInTheDocument();
  });

  it('applies size classes (default md)', () => {
    render(<Spinner />);
    expect(screen.getByRole('status')).toHaveClass('h-8', 'w-8');
  });

  it('applies sm and lg size classes', () => {
    const { rerender } = render(<Spinner size="sm" />);
    expect(screen.getByRole('status')).toHaveClass('h-4', 'w-4');
    rerender(<Spinner size="lg" />);
    expect(screen.getByRole('status')).toHaveClass('h-10', 'w-10');
  });

  it('defaults to the brand tone and supports the current tone', () => {
    const { rerender } = render(<Spinner />);
    expect(screen.getByRole('status')).toHaveClass('border-brand-teal-500');
    rerender(<Spinner tone="current" />);
    const el = screen.getByRole('status');
    expect(el).toHaveClass('border-current');
    expect(el).not.toHaveClass('border-brand-teal-500');
  });

  it('suppresses animation under reduced motion', () => {
    render(<Spinner />);
    expect(screen.getByRole('status')).toHaveClass('motion-reduce:animate-none');
  });

  it('forwards data-testid and className', () => {
    render(<Spinner data-testid="my-spinner" className="my-class" />);
    const el = screen.getByTestId('my-spinner');
    expect(el).toBe(screen.getByRole('status'));
    expect(el).toHaveClass('my-class');
  });
});
