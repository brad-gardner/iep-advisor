import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import type { MeetingSummaryDto } from '@/features/shared-drafts/types';

const sharedDraftsApi = vi.hoisted(() => ({
  getMeetingSummary: vi.fn(),
  draftMeetingSummary: vi.fn(),
  updateMeetingSummary: vi.fn(),
  sendMeetingSummary: vi.fn(),
}));
vi.mock('@/features/shared-drafts/api/shared-drafts-api', () => sharedDraftsApi);

import { FamilySummaryPanel } from './family-summary-panel';

function makeSummary(overrides: Partial<MeetingSummaryDto> = {}): MeetingSummaryDto {
  return {
    id: 1,
    meetingId: 42,
    status: 'Draft',
    body: 'The team met and agreed to update the reading goal.',
    generatedAt: '2026-09-10T00:00:00.000Z',
    editedAt: null,
    sentAt: null,
    sentByName: null,
    recipients: [{ displayName: 'Jamie Parent', email: 'jamie@example.com' }],
    ...overrides,
  };
}

function renderPanel() {
  render(
    <ToastProvider>
      <FamilySummaryPanel meetingId={42} />
    </ToastProvider>
  );
}

describe('FamilySummaryPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('drafts a summary with AI when none exists yet', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(null);
    sharedDraftsApi.draftMeetingSummary.mockResolvedValue({ success: true, data: makeSummary() });
    renderPanel();

    const draftButton = await screen.findByTestId('family-summary-draft');
    await user.click(draftButton);

    await waitFor(() => expect(sharedDraftsApi.draftMeetingSummary).toHaveBeenCalledWith(42));
    expect(await screen.findByTestId('family-summary-textarea')).toHaveValue(
      'The team met and agreed to update the reading goal.'
    );
  });

  it('edits the draft, then sends it after confirming the recipient list', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(makeSummary());
    sharedDraftsApi.updateMeetingSummary.mockResolvedValue({ success: true, data: makeSummary({ body: 'Updated summary text.' }) });
    sharedDraftsApi.sendMeetingSummary.mockResolvedValue({
      success: true,
      data: makeSummary({ status: 'Sent', body: 'Updated summary text.', sentAt: '2026-09-11T00:00:00.000Z', sentByName: 'Case Manager' }),
    });
    renderPanel();

    const textarea = await screen.findByTestId('family-summary-textarea');
    await user.clear(textarea);
    await user.type(textarea, 'Updated summary text.');

    await user.click(screen.getByTestId('family-summary-send-open'));
    expect(await screen.findByTestId('family-summary-send-confirm')).toHaveTextContent('Jamie Parent');

    await user.click(screen.getByTestId('family-summary-send-confirm-confirm'));

    await waitFor(() => expect(sharedDraftsApi.updateMeetingSummary).toHaveBeenCalledWith(42, 'Updated summary text.'));
    await waitFor(() => expect(sharedDraftsApi.sendMeetingSummary).toHaveBeenCalledWith(42));
    expect(await screen.findByTestId('family-summary-sent')).toHaveTextContent('Updated summary text.');
  });

  it('blocks Send while a Save is still in flight (one write to the summary at a time)', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(makeSummary());
    let finishSave!: (value: { success: boolean; data: MeetingSummaryDto }) => void;
    sharedDraftsApi.updateMeetingSummary.mockReturnValue(new Promise((resolve) => (finishSave = resolve)));
    renderPanel();

    await screen.findByTestId('family-summary-textarea');
    await user.click(screen.getByTestId('family-summary-save'));
    expect(screen.getByTestId('family-summary-send-open')).toBeDisabled();

    await act(async () => finishSave({ success: true, data: makeSummary() }));
    expect(screen.getByTestId('family-summary-send-open')).not.toBeDisabled();
  });

  it('renders the sent summary markdown as formatted HTML', async () => {
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(
      makeSummary({
        status: 'Sent',
        body: 'The team **agreed** to update the reading goal.',
        sentAt: '2026-09-11T00:00:00.000Z',
        sentByName: 'Case Manager',
      })
    );
    renderPanel();

    const body = await screen.findByTestId('family-summary-sent-body');
    expect(body.querySelector('strong')).toHaveTextContent('agreed');
    expect(body).toHaveTextContent('The team agreed to update the reading goal.');
  });

  it('surfaces a server refusal from a failed send', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(makeSummary());
    sharedDraftsApi.updateMeetingSummary.mockRejectedValue(apiRejection('Could not save your edits.'));
    renderPanel();

    await screen.findByTestId('family-summary-textarea');
    await user.click(screen.getByTestId('family-summary-send-open'));
    await user.click(screen.getByTestId('family-summary-send-confirm-confirm'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not save your edits.');
  });
});
