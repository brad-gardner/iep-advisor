import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

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
      engagementError: 'Engagement is down',
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
      adoptionError: 'Adoption is down',
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
      adoptionError: 'Adoption is down',
      engagementError: 'Engagement is down',
      isLoading: false,
      error: 'Adoption is down',
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
