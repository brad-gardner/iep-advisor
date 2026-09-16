import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { User } from '@/types/api';
import { makeHomeDto, makeParentHome } from '../test/fixtures';

const useAuthMock = vi.fn();
vi.mock('@/features/auth/hooks/use-auth', () => ({
  useAuth: () => useAuthMock(),
}));

const useHomeMock = vi.fn();
vi.mock('../hooks/use-home', () => ({
  useHome: () => useHomeMock(),
}));

vi.mock('@/features/children/components/dashboard-children-section', () => ({
  DashboardChildrenSection: () => <div data-testid="dashboard-children-section" />,
}));

const meetingsApi = vi.hoisted(() => ({ rsvpToMeeting: vi.fn() }));
vi.mock('@/features/meetings/api/meetings-api', () => meetingsApi);

import { ParentHomePage } from './parent-home-page';

function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    email: 'priya@example.com',
    firstName: 'Priya',
    lastName: 'Parent',
    state: 'OH',
    role: 'Parent',
    fullName: 'Priya Parent',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ParentHomePage />
    </MemoryRouter>
  );
}

describe('ParentHomePage', () => {
  beforeEach(() => {
    useAuthMock.mockReset();
    useHomeMock.mockReset();
    meetingsApi.rsvpToMeeting.mockReset();
    useAuthMock.mockReturnValue({ user: makeUser() });
  });

  it('shows the legacy Mode-C body unchanged for a parent with no school-linked child', () => {
    useHomeMock.mockReturnValue({
      home: makeHomeDto({
        kind: 'Parent',
        staff: undefined,
        parent: makeParentHome({
          children: [{ childId: 1, childName: 'Ada', hasSchoolLink: false, studentId: null }],
        }),
      }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderPage();

    expect(screen.getByTestId('parent-home-legacy')).toBeInTheDocument();
    expect(screen.getByTestId('dashboard-children-section')).toBeInTheDocument();
    expect(screen.queryByTestId('parent-home-operational')).not.toBeInTheDocument();
  });

  it('shows the onboarding banner in Mode C for an incomplete onboarding', () => {
    useAuthMock.mockReturnValue({ user: makeUser({ onboardingCompleted: false }) });
    useHomeMock.mockReturnValue({
      home: makeHomeDto({
        kind: 'Parent',
        staff: undefined,
        parent: makeParentHome({ children: [] }),
      }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderPage();

    expect(screen.getByTestId('onboarding-banner')).toBeInTheDocument();
  });

  it('shows the next meeting with a countdown and lets the parent RSVP', async () => {
    const user = userEvent.setup();
    meetingsApi.rsvpToMeeting.mockResolvedValue({ success: true, data: { myInviteStatus: 'Accepted' } });
    useHomeMock.mockReturnValue({
      home: makeHomeDto({
        kind: 'Parent',
        staff: undefined,
        parent: makeParentHome({
          nextMeeting: {
            id: 55,
            title: 'Annual review',
            type: 'AnnualReview',
            startsAtUtc: '2099-01-01T15:00:00.000Z',
            timeZoneId: 'America/New_York',
            durationMinutes: 60,
            studentId: 10,
            studentName: 'Ada Lovelace',
            myInviteStatus: 'Pending',
            status: 'Scheduled',
            childId: 1,
            childName: 'Ada Lovelace',
            daysUntil: 5,
          },
        }),
      }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderPage();

    expect(screen.getByTestId('parent-home-next-meeting')).toBeInTheDocument();
    expect(screen.getByText('In 5 days')).toBeInTheDocument();
    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument();

    await user.click(screen.getByTestId('next-meeting-accept'));
    expect(meetingsApi.rsvpToMeeting).toHaveBeenCalledWith(55, { status: 'Accepted' });
  });

  it('shows an empty hint (not an error) when there is no upcoming meeting', () => {
    useHomeMock.mockReturnValue({
      home: makeHomeDto({ kind: 'Parent', staff: undefined, parent: makeParentHome() }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderPage();

    expect(screen.getByTestId('parent-home-next-meeting-empty')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('links documents to review to the server-provided linkPath', () => {
    useHomeMock.mockReturnValue({
      home: makeHomeDto({
        kind: 'Parent',
        staff: undefined,
        parent: makeParentHome({
          documentsToReview: [
            {
              kind: 'Finalized',
              id: 9,
              childId: 1,
              childName: 'Ada Lovelace',
              documentTypeDisplayName: 'IEP',
              versionNumber: 3,
              date: '2026-09-01',
              linkPath: '/children/1/authored-versions/9',
            },
          ],
        }),
      }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderPage();

    const row = screen.getByTestId('home-documents-to-review-Finalized-9');
    expect(row.closest('a')).toHaveAttribute('href', '/children/1/authored-versions/9');
  });

  it('demotes setup notices below the operational sections for a school-linked parent', () => {
    useAuthMock.mockReturnValue({ user: makeUser({ onboardingCompleted: false }) });
    useHomeMock.mockReturnValue({
      home: makeHomeDto({
        kind: 'Parent',
        staff: undefined,
        parent: makeParentHome({ setupNotices: ['No school link yet for Bob'] }),
      }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderPage();

    expect(screen.getByTestId('parent-home-operational')).toBeInTheDocument();
    expect(screen.getByTestId('parent-home-setup-notices')).toBeInTheDocument();
    expect(screen.getByText('No school link yet for Bob')).toBeInTheDocument();
    expect(screen.getByTestId('onboarding-banner')).toBeInTheDocument();
  });

  it('shows an error notice with retry on load failure', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    useHomeMock.mockReturnValue({ home: null, isLoading: false, error: 'Boom', retry });
    renderPage();

    expect(screen.getByTestId('parent-home-error')).toBeInTheDocument();
    await user.click(screen.getByTestId('parent-home-retry'));
    expect(retry).toHaveBeenCalledTimes(1);
  });
});
