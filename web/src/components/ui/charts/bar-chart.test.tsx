import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BarChart } from './bar-chart';

describe('BarChart', () => {
  const data = [
    { label: 'Lincoln Elementary', value: 8 },
    { label: 'Roosevelt Middle', value: 3 },
  ];

  it('exposes an accessible name via role=img + aria-label', () => {
    render(<BarChart title="Overdue by school" data={data} data-testid="chart" />);
    expect(screen.getByRole('img', { name: 'Overdue by school' })).toBeInTheDocument();
  });

  it('renders a visually-hidden table fallback with the exact values', () => {
    render(<BarChart title="Overdue by school" data={data} />);
    const table = screen.getByRole('table', { name: 'Overdue by school' });
    expect(table.className).toContain('sr-only');
    expect(screen.getByRole('row', { name: /Lincoln Elementary\s+8/ })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /Roosevelt Middle\s+3/ })).toBeInTheDocument();
  });

  it('formats values with a custom formatter in the table', () => {
    render(<BarChart title="Staff active" data={data} valueFormat={(v) => `${v} staff`} />);
    expect(screen.getByRole('row', { name: /Lincoln Elementary\s+8 staff/ })).toBeInTheDocument();
  });

  it('renders zero rows without throwing', () => {
    render(<BarChart title="Empty" data={[]} data-testid="empty-chart" />);
    expect(screen.getByTestId('empty-chart')).toBeInTheDocument();
  });
});
