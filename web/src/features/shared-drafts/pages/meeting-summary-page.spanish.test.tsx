import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { MeetingSummaryDto } from '../types';

const sharedDraftsApi = vi.hoisted(() => ({ getMeetingSummary: vi.fn() }));
vi.mock('../api/shared-drafts-api', () => sharedDraftsApi);

import { MeetingSummaryPage } from './meeting-summary-page';

function makeSummary(overrides: Partial<MeetingSummaryDto> = {}): MeetingSummaryDto {
  return {
    id: 1,
    meetingId: 42,
    status: 'Sent',
    body: 'The team met and agreed to update the reading goal.',
    generatedAt: '2026-09-10T00:00:00.000Z',
    editedAt: null,
    sentAt: '2026-09-11T00:00:00.000Z',
    sentByName: 'Case Manager',
    recipients: [],
    ...overrides,
  };
}

function renderPage() {
  return renderInSpanish(
    <MemoryRouter initialEntries={['/children/3/meetings/42/summary']}>
      <Routes>
        <Route path="/children/:childId/meetings/:meetingId/summary" element={<MeetingSummaryPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('MeetingSummaryPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders a sent summary in Spanish, with a generated-language notice for an English artifact', async () => {
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(makeSummary({ generatedLanguage: 'en' }));

    await renderPage();

    expect(await screen.findByText(/Enviado el.*por Case Manager/)).toBeInTheDocument();
    expect(screen.getByTestId('generated-language-notice')).toHaveTextContent('Generado en inglés');
    expect(screen.getByTestId('meeting-summary-body')).toHaveTextContent(
      'The team met and agreed to update the reading goal.'
    );
  });

  it('renders the empty state in Spanish when no summary has been sent yet', async () => {
    sharedDraftsApi.getMeetingSummary.mockResolvedValue(null);

    await renderPage();

    expect(await screen.findByText('Aún no hay resumen')).toBeInTheDocument();
    expect(screen.getByText("La escuela de su hijo aún no ha enviado un resumen de esta reunión.")).toBeInTheDocument();
  });
});
