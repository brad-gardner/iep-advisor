import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Skeleton } from './skeleton';

describe('Skeleton', () => {
  it('renders and forwards data-testid', () => {
    render(<Skeleton data-testid="sk" />);
    expect(screen.getByTestId('sk')).toBeInTheDocument();
  });

  it('is decorative (aria-hidden)', () => {
    render(<Skeleton data-testid="sk" />);
    expect(screen.getByTestId('sk')).toHaveAttribute('aria-hidden', 'true');
  });

  it('applies the passed className for sizing', () => {
    render(<Skeleton data-testid="sk" className="h-4 w-40" />);
    expect(screen.getByTestId('sk')).toHaveClass('h-4', 'w-40');
  });

  it('suppresses the pulse under reduced motion', () => {
    render(<Skeleton data-testid="sk" />);
    expect(screen.getByTestId('sk')).toHaveClass('motion-reduce:animate-none');
  });

  it('renders a circle variant', () => {
    render(<Skeleton data-testid="sk" circle />);
    expect(screen.getByTestId('sk')).toHaveClass('rounded-full');
  });
});
