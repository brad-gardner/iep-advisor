import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import { makeMeeting, makeParticipant } from '../test/fixtures';

const meetingsApi = vi.hoisted(() => ({
  rsvpToMeeting: vi.fn(),
  cancelMeeting: vi.fn(),
  setMeetingStatus: vi.fn(),
  recordAttendance: vi.fn(),
  meetingIcsUrl: (id: number) => `/api/meetings/${id}.ics`,
  createMeeting: vi.fn(),
  updateMeeting: vi.fn(),
  getDefaultParticipants: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('../api/meetings-api', () => meetingsApi);

const educatorApi = vi.hoisted(() => ({
  getEligibleTeamStaff: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

const sharedDraftsApi = vi.hoisted(() => ({
  getMeetingSummary: vi.fn().mockResolvedValue(null),
  draftMeetingSummary: vi.fn(),
  updateMeetingSummary: vi.fn(),
  sendMeetingSummary: vi.fn(),
}));
vi.mock('@/features/shared-drafts/api/shared-drafts-api', () => sharedDraftsApi);

import { MeetingDrawer } from './meeting-drawer';

function renderDrawer(meeting = makeMeeting(), onUpdated = vi.fn()) {
  const onClose = vi.fn();
  render(
    <ToastProvider>
      <MemoryRouter>
        <MeetingDrawer open meeting={meeting} onClose={onClose} onUpdated={onUpdated} />
      </MemoryRouter>
    </ToastProvider>
  );
  return { onClose, onUpdated };
}

describe('MeetingDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders meeting details and the participant roster', () => {
    renderDrawer();
    expect(screen.getByTestId('meeting-drawer')).toHaveTextContent('Annual review meeting');
    expect(screen.getByTestId('meeting-participant-list')).toHaveTextContent('Casey Manager');
  });

  it('shows my RSVP buttons only when I am a participant', () => {
    renderDrawer(makeMeeting({ myInviteStatus: null }));
    expect(screen.queryByTestId('meeting-rsvp-accept')).not.toBeInTheDocument();
  });

  it('accepts an RSVP and surfaces the updated meeting', async () => {
    const user = userEvent.setup();
    const updated = makeMeeting({ myInviteStatus: 'Accepted' });
    meetingsApi.rsvpToMeeting.mockResolvedValue({ success: true, data: updated });
    const { onUpdated } = renderDrawer(makeMeeting({ myInviteStatus: 'Pending' }));

    await user.click(screen.getByTestId('meeting-rsvp-accept'));
    await waitFor(() => expect(meetingsApi.rsvpToMeeting).toHaveBeenCalledWith(100, { status: 'Accepted' }));
    expect(onUpdated).toHaveBeenCalledWith(updated);
  });

  it('surfaces a server refusal from a failed RSVP inline', async () => {
    const user = userEvent.setup();
    meetingsApi.rsvpToMeeting.mockRejectedValue(apiRejection('This invitation has expired.'));
    renderDrawer(makeMeeting({ myInviteStatus: 'Pending' }));

    await user.click(screen.getByTestId('meeting-rsvp-decline'));
    expect(await screen.findByRole('alert')).toHaveTextContent('This invitation has expired.');
  });

  it('cancels a meeting with a reason via the confirm dialog', async () => {
    const user = userEvent.setup();
    const cancelled = makeMeeting({ status: 'Cancelled' });
    meetingsApi.cancelMeeting.mockResolvedValue({ success: true, data: cancelled });
    const { onUpdated } = renderDrawer();

    await user.click(screen.getByTestId('meeting-cancel-open'));
    await user.type(screen.getByLabelText('Reason'), 'Family conflict');
    await user.click(screen.getByTestId('meeting-cancel-dialog-confirm'));

    await waitFor(() =>
      expect(meetingsApi.cancelMeeting).toHaveBeenCalledWith(100, { reason: 'Family conflict' })
    );
    expect(onUpdated).toHaveBeenCalledWith(cancelled);
  });

  it('surfaces a cancel refusal inside the confirm dialog', async () => {
    const user = userEvent.setup();
    meetingsApi.cancelMeeting.mockRejectedValue(apiRejection('Only the organizer can cancel this meeting.'));
    renderDrawer();

    await user.click(screen.getByTestId('meeting-cancel-open'));
    await user.click(screen.getByTestId('meeting-cancel-dialog-confirm'));

    expect(await screen.findByText('Only the organizer can cancel this meeting.')).toBeInTheDocument();
  });

  it('changes meeting status for a manager', async () => {
    const user = userEvent.setup();
    const held = makeMeeting({ status: 'Held' });
    meetingsApi.setMeetingStatus.mockResolvedValue({ success: true, data: held });
    renderDrawer();

    await user.selectOptions(screen.getByLabelText('Status'), 'Held');
    await waitFor(() =>
      expect(meetingsApi.setMeetingStatus).toHaveBeenCalledWith(100, { status: 'Held' })
    );
  });

  it('captures attendance when the meeting is Held and saves it', async () => {
    const user = userEvent.setup();
    const meeting = makeMeeting({
      status: 'Held',
      participants: [makeParticipant({ id: 9, attended: null })],
    });
    meetingsApi.recordAttendance.mockResolvedValue({ success: true, data: meeting });
    renderDrawer(meeting);

    expect(screen.getByTestId('attendance-checkbox-9')).toBeInTheDocument();
    await user.click(screen.getByTestId('attendance-checkbox-9'));
    await user.click(screen.getByTestId('meeting-save-attendance'));

    await waitFor(() =>
      expect(meetingsApi.recordAttendance).toHaveBeenCalledWith(100, {
        attendance: [{ participantId: 9, attended: true, excusalNote: undefined }],
      })
    );
  });

  it('remounts the family summary panel when the selected meeting changes', async () => {
    const user = userEvent.setup();
    const summaryFor = (meetingId: number, body: string) => ({
      id: meetingId,
      meetingId,
      status: 'Draft' as const,
      body,
      generatedAt: '2026-09-10T00:00:00.000Z',
      editedAt: null,
      sentAt: null,
      sentByName: null,
      recipients: [],
    });
    sharedDraftsApi.getMeetingSummary.mockImplementation(async (id: number) =>
      id === 100 ? summaryFor(100, 'Summary for meeting A') : summaryFor(101, 'Summary for meeting B')
    );
    const meetingA = makeMeeting({ id: 100, status: 'Held' });
    const meetingB = makeMeeting({ id: 101, status: 'Held', title: 'Meeting B' });
    const { rerender } = render(
      <ToastProvider>
        <MemoryRouter>
          <MeetingDrawer open meeting={meetingA} onClose={vi.fn()} onUpdated={vi.fn()} />
        </MemoryRouter>
      </ToastProvider>
    );
    const textarea = await screen.findByTestId('family-summary-textarea');
    expect(textarea).toHaveValue('Summary for meeting A');
    await user.type(textarea, ' plus an unsaved edit');

    // The parent swaps `meeting` in place (one drawer, selection changes): the panel must
    // start over for meeting B rather than carry A's text under B's id.
    rerender(
      <ToastProvider>
        <MemoryRouter>
          <MeetingDrawer open meeting={meetingB} onClose={vi.fn()} onUpdated={vi.fn()} />
        </MemoryRouter>
      </ToastProvider>
    );
    expect(screen.queryByDisplayValue(/plus an unsaved edit/)).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('family-summary-textarea')).toHaveValue('Summary for meeting B'));
  });

  it('does not offer attendance capture before the meeting is held', () => {
    renderDrawer(makeMeeting({ status: 'Scheduled' }));
    expect(screen.queryByTestId('meeting-save-attendance')).not.toBeInTheDocument();
  });

  it('copies the ICS link to the clipboard', async () => {
    // `userEvent.setup()` installs testing-library's real Clipboard stub, so
    // this exercises the actual write/read path rather than a bare mock.
    const user = userEvent.setup();
    renderDrawer();
    await user.click(screen.getByTestId('meeting-copy-ics'));
    await waitFor(async () => {
      expect(await navigator.clipboard.readText()).toContain('/api/meetings/100.ics');
    });
  });

  it('hides manager controls for a non-manager participant', () => {
    renderDrawer(makeMeeting({ canManage: false }));
    expect(screen.queryByTestId('meeting-cancel-open')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Status')).not.toBeInTheDocument();
  });
});
