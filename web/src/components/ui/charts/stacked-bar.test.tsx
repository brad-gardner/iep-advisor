import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StackedBarChart, type StackedBarRow } from './stacked-bar';

const rows: StackedBarRow[] = [
  {
    label: 'Lincoln Elementary',
    segments: [
      { key: 'overdueAnnual', label: 'Overdue annual', value: 4 },
      { key: 'due30', label: 'Due 30 days', value: 2 },
    ],
  },
  {
    label: 'Roosevelt Middle',
    segments: [
      { key: 'overdueAnnual', label: 'Overdue annual', value: 1 },
      { key: 'due30', label: 'Due 30 days', value: 5 },
    ],
  },
];

describe('StackedBarChart', () => {
  it('exposes an accessible name via role=img + aria-label', () => {
    render(<StackedBarChart title="Deadline buckets by school" rows={rows} />);
    expect(screen.getByRole('img', { name: 'Deadline buckets by school' })).toBeInTheDocument();
  });

  it('renders a text legend, never colour-only', () => {
    render(<StackedBarChart title="Buckets" rows={rows} />);
    // Appears once in the visible legend and once in the hidden table header.
    expect(screen.getAllByText('Overdue annual').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Due 30 days').length).toBeGreaterThan(0);
  });

  it('renders a visually-hidden table with the full per-segment breakdown', () => {
    render(<StackedBarChart title="Buckets" rows={rows} data-testid="stacked" />);
    const table = screen.getByRole('table', { name: 'Buckets' });
    expect(table.className).toContain('sr-only');
    expect(screen.getByRole('row', { name: /Lincoln Elementary\s+4\s+2/ })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /Roosevelt Middle\s+1\s+5/ })).toBeInTheDocument();
  });

  it('renders zero rows without throwing', () => {
    render(<StackedBarChart title="Empty" rows={[]} data-testid="empty-stacked" />);
    expect(screen.getByTestId('empty-stacked')).toBeInTheDocument();
  });
});
