import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { makeMeeting } from '../test/fixtures';

const meetingsApi = vi.hoisted(() => ({
  getMeetingByToken: vi.fn(),
  submitTokenRsvp: vi.fn(),
}));
vi.mock('../api/meetings-api', () => meetingsApi);

import { MeetingRsvpPage } from './meeting-rsvp-page';

function renderPage(search = '?token=abc123') {
  return renderInSpanish(
    <MemoryRouter initialEntries={[`/meetings/rsvp${search}`]}>
      <Routes>
        <Route path="/meetings/rsvp" element={<MeetingRsvpPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('MeetingRsvpPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the invitation and RSVP actions in Spanish', async () => {
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting({ title: 'Revisión anual' }), status: 'Pending' },
    });

    await renderPage();

    expect(await screen.findByRole('heading', { name: 'Invitación a la reunión' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Aceptar/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Tal vez/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Rechazar/ })).toBeInTheDocument();
  });

  it('renders the recorded-answer view in Spanish', async () => {
    meetingsApi.getMeetingByToken.mockResolvedValue({
      success: true,
      data: { meeting: makeMeeting(), status: 'Accepted' },
    });

    await renderPage();

    expect(await screen.findByText('Gracias; su respuesta fue registrada')).toBeInTheDocument();
    expect(screen.getByText('Usted respondió: Aceptada.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cambiar respuesta' })).toBeInTheDocument();
  });

  it('renders the missing-token error in Spanish', async () => {
    await renderPage('');

    expect(await screen.findByRole('alert')).toHaveTextContent('A este enlace le falta su token de invitación.');
  });
});
