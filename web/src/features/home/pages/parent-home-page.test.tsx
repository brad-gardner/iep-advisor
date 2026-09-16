import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, render, screen } from '@testing-library/react';
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
    // The countdown is now recomputed client-side from `startsAtUtc` (never
    // trusted as a static server snapshot — see `next-meeting-card.tsx`), so
    // the clock is pinned and the fixture's timestamp is relative to it. Both
    // are built from local wall-clock components so the 5-day gap holds
    // regardless of the host machine's time zone.
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      vi.setSystemTime(new Date(2026, 8, 16, 12, 0));
      const user = userEvent.setup({ advanceTimers: (ms) => vi.advanceTimersByTime(ms), delay: null });
      meetingsApi.rsvpToMeeting.mockResolvedValue({ success: true, data: { myInviteStatus: 'Accepted' } });
      useHomeMock.mockReturnValue({
        home: makeHomeDto({
          kind: 'Parent',
          parent: makeParentHome({
            nextMeeting: {
              id: 55,
              title: 'Annual review',
              type: 'AnnualReview',
              startsAtUtc: new Date(2026, 8, 21, 12, 0).toISOString(),
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
    } finally {
      act(() => {
        vi.runOnlyPendingTimers();
      });
      vi.useRealTimers();
    }
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
    expect(screen.getByRole('alert')).toHaveTextContent('Boom');
    await user.click(screen.getByTestId('parent-home-retry'));
    expect(retry).toHaveBeenCalledTimes(1);
  });
});
