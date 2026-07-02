import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Users } from 'lucide-react';
import { EmptyState } from './empty-state';

describe('EmptyState', () => {
  it('renders the title and description', () => {
    render(<EmptyState title="No staff yet" description="Invite someone to get started." />);
    expect(screen.getByRole('heading', { name: 'No staff yet' })).toBeInTheDocument();
    expect(screen.getByText('Invite someone to get started.')).toBeInTheDocument();
  });

  it('renders an action node', () => {
    render(
      <EmptyState
        title="No staff yet"
        action={<button type="button">Invite</button>}
      />
    );
    expect(screen.getByRole('button', { name: 'Invite' })).toBeInTheDocument();
  });

  it('renders a lucide icon component passed as icon', () => {
    render(<EmptyState icon={Users} title="No staff yet" />);
    // lucide renders an <svg>; it is decorative (aria-hidden).
    expect(document.querySelector('svg[aria-hidden="true"]')).toBeInTheDocument();
  });

  it('forwards data-testid', () => {
    render(<EmptyState data-testid="empty" title="No staff yet" />);
    expect(screen.getByTestId('empty')).toBeInTheDocument();
  });
});
