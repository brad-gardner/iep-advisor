import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
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
  return renderInSpanish(
    <ToastProvider>
      <FamilySummaryPanel meetingId={42} />
    </ToastProvider>
  );
}

describe('FamilySummaryPanel in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and draft action in Spanish when no summary exists yet', async () => {
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(null);

    await renderPanel();

    expect(await screen.findByText('Resumen familiar')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Redactar con IA' })).toBeInTheDocument();
  });

  it("tells a Spanish-viewing staff member the sent summary was generated in English", async () => {
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(
      makeSummary({
        status: 'Sent',
        sentAt: '2026-09-11T00:00:00.000Z',
        sentByName: 'Case Manager',
        generatedLanguage: 'en',
      })
    );

    await renderPanel();

    await screen.findByTestId('family-summary-sent');
    expect(screen.getByTestId('generated-language-notice')).toHaveTextContent('Generado en inglés');
    expect(screen.getByText(/Enviado el.*por Case Manager/)).toBeInTheDocument();
  });
});
