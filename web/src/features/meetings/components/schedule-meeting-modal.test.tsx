import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import { makeMeeting, makeParticipant } from '../test/fixtures';

const meetingsApi = vi.hoisted(() => ({
  createMeeting: vi.fn(),
  updateMeeting: vi.fn(),
  getDefaultParticipants: vi.fn(),
}));
vi.mock('../api/meetings-api', () => meetingsApi);

const educatorApi = vi.hoisted(() => ({
  getEligibleTeamStaff: vi.fn(),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

import { ScheduleMeetingModal } from './schedule-meeting-modal';

const defaults = [
  { userId: 7, displayName: 'Casey Manager', email: 'casey@district.org', teamRole: 'CaseManager' as const, isFamily: false, isStudent: false },
  { userId: 41, displayName: 'Pat Parent', email: 'parent@example.com', teamRole: 'Other' as const, isFamily: true, isStudent: false },
  { userId: 52, displayName: 'Jordan Ellis', email: 'kid@example.com', teamRole: 'Other' as const, isFamily: false, isStudent: true },
];

const eligible = [
  {
    staffProfileId: 80,
    userId: 8,
    firstName: 'Dana',
    lastName: 'Speech',
    email: 'dana@district.org',
    orgRoleId: 4,
    orgRoleName: 'RelatedServiceProvider',
    schoolId: 5,
    schoolName: 'Lincoln Elementary',
  },
];

function renderModal(props: Partial<React.ComponentProps<typeof ScheduleMeetingModal>> = {}) {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  render(
    <ToastProvider>
      <ScheduleMeetingModal
        open
        onClose={onClose}
        studentId={10}
        studentName="Ada Lovelace"
        onSaved={onSaved}
        {...props}
      />
    </ToastProvider>
  );
  return { onClose, onSaved };
}

describe('ScheduleMeetingModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    meetingsApi.getDefaultParticipants.mockResolvedValue({ success: true, data: defaults });
    educatorApi.getEligibleTeamStaff.mockResolvedValue({ success: true, data: eligible });
  });

  it('pre-checks the server defaults (team, family, student) as real users, leaving eligible staff unchecked', async () => {
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    expect(screen.getByTestId('participant-checkbox-team-7')).toBeChecked();
    expect(screen.getByTestId('participant-checkbox-family-41')).toBeChecked();
    expect(screen.getByTestId('participant-checkbox-student-52')).toBeChecked();
    expect(screen.getByTestId('participant-checkbox-eligible-80')).not.toBeChecked();
    expect(screen.getByText('Family')).toBeInTheDocument();
    expect(screen.getByText('Student')).toBeInTheDocument();
  });

  it('converts the chosen local date/time in America/New_York (winter, EST) to UTC on submit', async () => {
    const user = userEvent.setup();
    meetingsApi.createMeeting.mockResolvedValue({ success: true, data: makeMeeting() });
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    await user.type(screen.getByLabelText('Date'), '2026-01-15');
    await user.type(screen.getByLabelText('Time'), '14:00');
    await user.selectOptions(screen.getByLabelText('Time zone'), 'America/New_York');
    await user.click(screen.getByTestId('schedule-meeting-submit'));

    await waitFor(() => expect(meetingsApi.createMeeting).toHaveBeenCalled());
    const [studentId, payload] = meetingsApi.createMeeting.mock.calls[0];
    expect(studentId).toBe(10);
    expect(payload.startsAtUtc).toBe('2026-01-15T19:00:00.000Z');
  });

  it('converts a summer date (EDT) to UTC — proves DST is honored across the modal', async () => {
    const user = userEvent.setup();
    meetingsApi.createMeeting.mockResolvedValue({ success: true, data: makeMeeting() });
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    await user.type(screen.getByLabelText('Date'), '2026-07-15');
    await user.type(screen.getByLabelText('Time'), '14:00');
    await user.selectOptions(screen.getByLabelText('Time zone'), 'America/New_York');
    await user.click(screen.getByTestId('schedule-meeting-submit'));

    await waitFor(() => expect(meetingsApi.createMeeting).toHaveBeenCalled());
    const [, payload] = meetingsApi.createMeeting.mock.calls[0];
    expect(payload.startsAtUtc).toBe('2026-07-15T18:00:00.000Z');
  });

  it('sends only checked participants, respecting an unchecked required toggle', async () => {
    const user = userEvent.setup();
    meetingsApi.createMeeting.mockResolvedValue({ success: true, data: makeMeeting() });
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    // Check the eligible staff member and mark them optional.
    await user.click(screen.getByTestId('participant-checkbox-eligible-80'));
    await user.click(screen.getByTestId('participant-required-eligible-80'));
    // Uncheck the family member.
    await user.click(screen.getByTestId('participant-checkbox-family-41'));

    await user.type(screen.getByLabelText('Date'), '2026-01-15');
    await user.type(screen.getByLabelText('Time'), '14:00');
    await user.click(screen.getByTestId('schedule-meeting-submit'));

    await waitFor(() => expect(meetingsApi.createMeeting).toHaveBeenCalled());
    const [, payload] = meetingsApi.createMeeting.mock.calls[0];
    expect(payload.participants).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ userId: 7, isRequired: true }),
        expect.objectContaining({ userId: 8, isRequired: false }),
      ])
    );
    expect(payload.participants).not.toEqual(
      expect.arrayContaining([expect.objectContaining({ userId: 41 })])
    );
  });

  it('adds an external participant row and includes it in the submitted payload', async () => {
    const user = userEvent.setup();
    meetingsApi.createMeeting.mockResolvedValue({ success: true, data: makeMeeting() });
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    await user.type(screen.getByLabelText('External participant name'), 'Guest Advocate');
    await user.type(screen.getByLabelText('Email'), 'guest@example.com');
    await user.click(screen.getByTestId('add-external-participant'));

    await user.type(screen.getByLabelText('Date'), '2026-01-15');
    await user.type(screen.getByLabelText('Time'), '14:00');
    await user.click(screen.getByTestId('schedule-meeting-submit'));

    await waitFor(() => expect(meetingsApi.createMeeting).toHaveBeenCalled());
    const [, payload] = meetingsApi.createMeeting.mock.calls[0];
    expect(payload.participants).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ externalName: 'Guest Advocate', externalEmail: 'guest@example.com' }),
      ])
    );
  });

  it('requires a date and time before submitting', async () => {
    const user = userEvent.setup();
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    await user.click(screen.getByTestId('schedule-meeting-submit'));
    expect(await screen.findByRole('alert')).toHaveTextContent('Choose a date and time');
    expect(meetingsApi.createMeeting).not.toHaveBeenCalled();
  });

  it('surfaces a server refusal inline and keeps the drawer open', async () => {
    const user = userEvent.setup();
    meetingsApi.createMeeting.mockRejectedValue(apiRejection('This time conflicts with another meeting.'));
    renderModal();
    await screen.findByTestId('participant-row-team-7');

    await user.type(screen.getByLabelText('Date'), '2026-01-15');
    await user.type(screen.getByLabelText('Time'), '14:00');
    await user.click(screen.getByTestId('schedule-meeting-submit'));

    expect(await screen.findByRole('alert')).toHaveTextContent('This time conflicts with another meeting.');
    expect(screen.getByTestId('schedule-meeting-modal')).toBeInTheDocument();
  });

  it('pre-fills from an existing meeting in reschedule mode and calls updateMeeting', async () => {
    const user = userEvent.setup();
    const existing = makeMeeting({
      id: 55,
      startsAtUtc: '2026-01-15T19:00:00.000Z',
      timeZoneId: 'America/New_York',
      participants: [makeParticipant({ id: 9, userId: 7, teamRole: 'CaseManager', isRequired: true })],
    });
    meetingsApi.updateMeeting.mockResolvedValue({ success: true, data: existing });
    renderModal({ meeting: existing });

    await screen.findByTestId('participant-row-team-7');
    expect(screen.getByLabelText('Date')).toHaveValue('2026-01-15');
    expect(screen.getByLabelText('Time')).toHaveValue('14:00');
    expect(screen.getByTestId('participant-checkbox-team-7')).toBeChecked();

    await user.click(screen.getByTestId('schedule-meeting-submit'));
    await waitFor(() => expect(meetingsApi.updateMeeting).toHaveBeenCalledWith(55, expect.any(Object)));
  });
});
