import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { apiRejection } from '@/test/axios-rejection';
import { makeMeeting } from '../test/fixtures';

const meetingsApi = vi.hoisted(() => ({
  getMeetingByToken: vi.fn(),
  submitTokenRsvp: vi.fn(),
}));
vi.mock('../api/meetings-api', () => meetingsApi);

import { MeetingRsvpPage } from './meeting-rsvp-page';

function renderPage(search = '?token=abc123') {
  return render(
    <MemoryRouter initialEntries={[`/meetings/rsvp${search}`]}>
      <Routes>
        <Route path="/meetings/rsvp" element={<MeetingRsvpPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('MeetingRsvpPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads the meeting summary from the token and shows RSVP buttons', async () => {
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting({ title: 'Annual review' }), status: 'Pending' },
    });
    renderPage();

    expect(await screen.findByText('Annual review')).toBeInTheDocument();
    expect(meetingsApi.getMeetingByToken).toHaveBeenCalledWith('abc123');
    expect(screen.getByTestId('rsvp-accept')).toBeInTheDocument();
  });

  it('submits Accept and shows a confirmation', async () => {
    const user = userEvent.setup();
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting(), status: 'Pending' },
    });
    meetingsApi.submitTokenRsvp.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting({ myInviteStatus: 'Accepted' }), status: 'Accepted' },
    });
    renderPage();
    await screen.findByTestId('rsvp-accept');

    await user.click(screen.getByTestId('rsvp-accept'));
    expect(meetingsApi.submitTokenRsvp).toHaveBeenCalledWith({ token: 'abc123', status: 'Accepted' });
    expect(await screen.findByText(/your response was recorded/i)).toBeInTheDocument();
    expect(screen.queryByTestId('rsvp-accept')).not.toBeInTheDocument();
  });

  it('shows an error for an expired or invalid token', async () => {
    meetingsApi.getMeetingByToken.mockRejectedValue(apiRejection('This invitation has expired.'));
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent('This invitation has expired.');
  });

  it('shows an error when the token is missing entirely', async () => {
    renderPage('');
    expect(await screen.findByRole('alert')).toHaveTextContent('missing its invitation token');
    expect(meetingsApi.getMeetingByToken).not.toHaveBeenCalled();
  });

  it('shows the recorded answer (not the prompt) when revisiting a link already responded to', async () => {
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting({ title: 'Annual review' }), status: 'Accepted' },
    });
    renderPage();

    expect(await screen.findByText(/your response was recorded/i)).toBeInTheDocument();
    expect(screen.getByText(/You responded: Accepted/)).toBeInTheDocument();
    expect(screen.queryByTestId('rsvp-accept')).not.toBeInTheDocument();

    const user = userEvent.setup();
    await user.click(screen.getByTestId('rsvp-change-response'));
    expect(screen.getByTestId('rsvp-accept')).toBeInTheDocument();
    expect(screen.queryByText(/your response was recorded/i)).not.toBeInTheDocument();
  });

  it('disables the whole RSVP group while one response is in flight', async () => {
    const user = userEvent.setup();
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting(), status: 'Pending' },
    });
    let resolveSubmit: (value: unknown) => void = () => {};
    meetingsApi.submitTokenRsvp.mockReturnValue(
      new Promise((resolve) => {
        resolveSubmit = resolve;
      })
    );
    renderPage();
    await screen.findByTestId('rsvp-accept');

    await user.click(screen.getByTestId('rsvp-accept'));
    expect(screen.getByTestId('rsvp-tentative')).toBeDisabled();
    expect(screen.getByTestId('rsvp-decline')).toBeDisabled();

    resolveSubmit({ success: true, data: { meeting: makeMeeting(), status: 'Accepted' } });
    await waitFor(() => expect(screen.queryByTestId('rsvp-accept')).not.toBeInTheDocument());
  });

  it('surfaces a failed RSVP submission inline', async () => {
    const user = userEvent.setup();
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting(), status: 'Pending' },
    });
    meetingsApi.submitTokenRsvp.mockRejectedValue(apiRejection('This meeting already started.'));
    renderPage();
    await screen.findByTestId('rsvp-decline');

    await user.click(screen.getByTestId('rsvp-decline'));
    expect(await screen.findByRole('alert')).toHaveTextContent('This meeting already started.');
  });
});
