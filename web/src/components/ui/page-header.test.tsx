import { describe, it, expect } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PageHeader } from './page-header';

function renderHeader(ui: React.ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

describe('PageHeader', () => {
  it('renders the title as the single h1', () => {
    renderHeader(<PageHeader title="Staff" />);
    const headings = screen.getAllByRole('heading', { level: 1 });
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent('Staff');
  });

  it('omits the subtitle when not provided', () => {
    renderHeader(<PageHeader title="Staff" />);
    expect(screen.queryByText('Manage your team')).not.toBeInTheDocument();
  });

  it('renders the subtitle when provided', () => {
    renderHeader(<PageHeader title="Staff" subtitle="Manage your team" />);
    expect(screen.getByText('Manage your team')).toBeInTheDocument();
  });

  it('omits the breadcrumb nav when not provided', () => {
    renderHeader(<PageHeader title="Staff" />);
    expect(screen.queryByRole('navigation', { name: 'Breadcrumb' })).not.toBeInTheDocument();
  });

  it('renders the breadcrumb nav with links and a current page', () => {
    renderHeader(
      <PageHeader
        title="Staff"
        breadcrumb={[
          { label: 'Home', to: '/home' },
          { label: 'Admin', to: '/admin' },
          { label: 'Staff' },
        ]}
      />
    );
    const nav = screen.getByRole('navigation', { name: 'Breadcrumb' });
    expect(within(nav).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/home');
    expect(within(nav).getByRole('link', { name: 'Admin' })).toBeInTheDocument();
    // Final crumb is plain text marked as the current page.
    const current = within(nav).getByText('Staff');
    expect(current).toHaveAttribute('aria-current', 'page');
  });

  it('renders the actions slot when provided', () => {
    renderHeader(
      <PageHeader
        title="Staff"
        actions={
          <>
            <button type="button">Cancel</button>
            <button type="button">Invite</button>
          </>
        }
      />
    );
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Invite' })).toBeInTheDocument();
  });

  it('omits the actions container when no actions are provided', () => {
    renderHeader(<PageHeader title="Staff" />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
