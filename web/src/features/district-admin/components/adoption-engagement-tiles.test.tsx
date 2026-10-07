import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
// `district-admin` is a staff-only namespace (plan phase 6) — this component
// renders behind the lazy district-admin route chunk in the real app, which
// registers its English as a side effect of importing `staff-locales`; this
// test renders the component directly, so it imports the same module itself.
import '@/app/lazy-routes/staff-locales';

const useAdoptionEngagementMock = vi.fn();
vi.mock('../hooks/use-adoption-engagement', () => ({
  useAdoptionEngagement: () => useAdoptionEngagementMock(),
}));

import { AdoptionEngagementTiles } from './adoption-engagement-tiles';

const adoptionDto = {
  days: 14,
  staffActiveLast14: 8,
  staffTotal: 10,
  bySchool: [],
  draftsStarted: 4,
  draftsFinalized: 2,
  activeRule: 'Logged in or edited a document in the window',
};

const engagementDto = {
  studentsWithFamilyLink: 40,
  activeStudents: 60,
  draftsShared: 0,
  responsesReceived: 0,
  bySchool: [],
};

function renderTiles() {
  return render(
    <MemoryRouter>
      <AdoptionEngagementTiles schoolId={null} />
    </MemoryRouter>
  );
}

describe('AdoptionEngagementTiles', () => {
  beforeEach(() => {
    useAdoptionEngagementMock.mockReset();
  });

  it('renders the adoption tiles alongside an inline alert when only engagement failed', () => {
    useAdoptionEngagementMock.mockReturnValue({
      adoption: adoptionDto,
      engagement: null,
      adoptionError: null,
      engagementError: { kind: 'server', message: 'Engagement is down' },
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderTiles();

    expect(screen.getByTestId('adoption-staff-active')).toBeInTheDocument();
    expect(screen.queryByTestId('engagement-family-linked')).not.toBeInTheDocument();
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Engagement is down');
  });

  it('renders the engagement tile alongside an inline alert when only adoption failed', () => {
    useAdoptionEngagementMock.mockReturnValue({
      adoption: null,
      engagement: engagementDto,
      adoptionError: { kind: 'server', message: 'Adoption is down' },
      engagementError: null,
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderTiles();

    expect(screen.getByTestId('engagement-family-linked')).toBeInTheDocument();
    expect(screen.queryByTestId('adoption-staff-active')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Adoption is down');
  });

  it('shows a single full-card alert when both endpoints failed', () => {
    useAdoptionEngagementMock.mockReturnValue({
      adoption: null,
      engagement: null,
      adoptionError: { kind: 'server', message: 'Adoption is down' },
      engagementError: { kind: 'server', message: 'Engagement is down' },
      isLoading: false,
      error: { kind: 'server', message: 'Adoption is down' },
      retry: vi.fn(),
    });
    renderTiles();

    expect(screen.getByTestId('adoption-engagement-error')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Adoption is down');
  });

  it('renders every tile with no alert when both endpoints succeed', () => {
    useAdoptionEngagementMock.mockReturnValue({
      adoption: adoptionDto,
      engagement: engagementDto,
      adoptionError: null,
      engagementError: null,
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderTiles();

    expect(screen.getByTestId('adoption-staff-active')).toBeInTheDocument();
    expect(screen.getByTestId('engagement-family-linked')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
