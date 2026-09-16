import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Sparkline } from './sparkline';

describe('Sparkline', () => {
  it('exposes an accessible name via role=img + aria-label', () => {
    render(<Sparkline title="Active staff, last 7 days" values={[2, 4, 3, 5, 6, 4, 7]} />);
    expect(screen.getByRole('img', { name: 'Active staff, last 7 days' })).toBeInTheDocument();
  });

  it('renders a visually-hidden table with the exact per-point values', () => {
    render(<Sparkline title="Trend" values={[1, 2, 3]} pointLabels={['Mon', 'Tue', 'Wed']} />);
    const table = screen.getByRole('table', { name: 'Trend' });
    expect(table.className).toContain('sr-only');
    expect(screen.getByRole('row', { name: /Mon\s+1/ })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /Tue\s+2/ })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /Wed\s+3/ })).toBeInTheDocument();
  });

  it('defaults point labels to a 1-based index when none are given', () => {
    render(<Sparkline title="Trend" values={[10, 20]} />);
    expect(screen.getByRole('row', { name: /1\s+10/ })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /2\s+20/ })).toBeInTheDocument();
  });

  it('renders an empty series without throwing', () => {
    render(<Sparkline title="Empty" values={[]} data-testid="empty-sparkline" />);
    expect(screen.getByTestId('empty-sparkline')).toBeInTheDocument();
  });
});
