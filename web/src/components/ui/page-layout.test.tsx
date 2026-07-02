import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PageLayout } from './page-layout';

function renderLayout(ui: React.ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

describe('PageLayout', () => {
  it('renders the title as an h1 and the children content region', () => {
    renderLayout(
      <PageLayout title="Staff">
        <p>Body content</p>
      </PageLayout>
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Staff' })).toBeInTheDocument();
    expect(screen.getByText('Body content')).toBeInTheDocument();
  });

  it('renders exactly one h1', () => {
    renderLayout(
      <PageLayout title="Staff">
        <div>child</div>
      </PageLayout>
    );
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('forwards data-testid to the outer element', () => {
    renderLayout(
      <PageLayout title="Staff" data-testid="district-staff-page">
        <div>child</div>
      </PageLayout>
    );
    const root = screen.getByTestId('district-staff-page');
    expect(root).toBeInTheDocument();
    expect(root).toContainElement(screen.getByRole('heading', { level: 1 }));
  });

  it('renders subtitle, breadcrumb, and actions slots together', () => {
    renderLayout(
      <PageLayout
        title="Staff"
        subtitle="Manage your team"
        breadcrumb={[{ label: 'Admin', to: '/admin' }, { label: 'Staff' }]}
        actions={<button type="button">Invite</button>}
      >
        <div>child</div>
      </PageLayout>
    );
    expect(screen.getByText('Manage your team')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Invite' })).toBeInTheDocument();
  });

  it('works with no optional slots (title only)', () => {
    renderLayout(<PageLayout title="Staff">content</PageLayout>);
    expect(screen.getByRole('heading', { level: 1, name: 'Staff' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Breadcrumb' })).not.toBeInTheDocument();
  });
});
