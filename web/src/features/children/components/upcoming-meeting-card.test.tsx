import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { makeMeeting } from '@/features/meetings/test/fixtures';

const meetingsApi = vi.hoisted(() => ({
  listChildMeetings: vi.fn(),
  rsvpToMeeting: vi.fn(),
}));
vi.mock('@/features/meetings/api/meetings-api', () => meetingsApi);

import { UpcomingMeetingCard } from './upcoming-meeting-card';

function renderCard(childId = 5) {
  return render(<UpcomingMeetingCard childId={childId} />);
}

describe('UpcomingMeetingCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the next upcoming meeting with RSVP buttons', async () => {
    meetingsApi.listChildMeetings.mockResolvedValue({
      success: true,
      data: [makeMeeting({ startsAtUtc: '2099-01-01T15:00:00.000Z', myInviteStatus: 'Pending' })],
    });
    renderCard();

    expect(await screen.findByTestId('upcoming-meeting-card')).toBeInTheDocument();
    expect(screen.getByTestId('upcoming-meeting-accept')).toBeInTheDocument();
  });

  it('disables the whole RSVP group while one response is in flight', async () => {
    const user = userEvent.setup();
    meetingsApi.listChildMeetings.mockResolvedValue({
      success: true,
      data: [makeMeeting({ startsAtUtc: '2099-01-01T15:00:00.000Z', myInviteStatus: 'Pending' })],
    });
    let resolveRsvp: (value: unknown) => void = () => {};
    meetingsApi.rsvpToMeeting.mockReturnValue(
      new Promise((resolve) => {
        resolveRsvp = resolve;
      })
    );
    renderCard();
    await screen.findByTestId('upcoming-meeting-accept');

    await user.click(screen.getByTestId('upcoming-meeting-accept'));
    expect(screen.getByTestId('upcoming-meeting-tentative')).toBeDisabled();
    expect(screen.getByTestId('upcoming-meeting-decline')).toBeDisabled();

    resolveRsvp({ success: true, data: makeMeeting({ myInviteStatus: 'Accepted' }) });
    await waitFor(() => expect(screen.getByTestId('upcoming-meeting-tentative')).not.toBeDisabled());
  });
});
