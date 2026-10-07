import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { ChildLinkInvitePreview } from '../types';

const childLinksApi = vi.hoisted(() => ({ previewLink: vi.fn(), acceptLink: vi.fn() }));
vi.mock('../api/child-links-api', () => childLinksApi);

import { AcceptLinkPage } from './accept-link-page';

function makePreview(overrides: Partial<ChildLinkInvitePreview> = {}): ChildLinkInvitePreview {
  return {
    schoolStudentId: 10,
    studentFirstName: 'Ada',
    studentLastName: 'Lovelace',
    schoolName: 'Riverside Elementary',
    existingChildren: [],
    ...overrides,
  };
}

function renderPage(token = 'abc123') {
  return renderInSpanish(
    <MemoryRouter initialEntries={[`/accept-link?token=${token}`]}>
      <AcceptLinkPage />
    </MemoryRouter>
  );
}

describe('AcceptLinkPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('translates the heading and the school-invite sentence, with the school and student names bold', async () => {
    childLinksApi.previewLink.mockResolvedValue({ success: true, data: makePreview() });
    await renderPage();

    expect(screen.getByRole('heading', { name: 'Vincúlese a su escuela' })).toBeInTheDocument();
    const sentence = await screen.findByText(/invitó a conectarse con/);
    expect(sentence).toHaveTextContent('Riverside Elementary le invitó a conectarse con Ada Lovelace.');
    expect(screen.getByRole('button', { name: 'Aceptar y vincular' })).toBeInTheDocument();
  });

  it('translates the no-school sentence when the invite has no school name', async () => {
    childLinksApi.previewLink.mockResolvedValue({
      success: true,
      data: makePreview({ schoolName: null }),
    });
    await renderPage();

    const sentence = await screen.findByText(/Le invitaron a conectarse con/);
    expect(sentence).toHaveTextContent('Le invitaron a conectarse con Ada Lovelace.');
  });

  it('translates the missing-token error', async () => {
    await renderPage('');

    expect(await screen.findByText('No se proporcionó un token de enlace.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir al panel' })).toBeInTheDocument();
  });
});
