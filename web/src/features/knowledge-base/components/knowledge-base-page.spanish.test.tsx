import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { KnowledgeBaseEntry } from '@/types/api';

const api = vi.hoisted(() => ({ searchKnowledgeBase: vi.fn(), getCategories: vi.fn(), getKnowledgeBaseEntry: vi.fn() }));
vi.mock('../api/knowledge-base-api', () => api);

import { KnowledgeBasePage } from './knowledge-base-page';

const entry = (id: number, title: string): KnowledgeBaseEntry => ({
  id,
  title,
  content: `About ${title}.`,
  category: 'rights',
  legalReference: null,
  state: null,
  tags: [],
});

function renderPage() {
  return renderInSpanish(
    <MemoryRouter initialEntries={['/knowledge-base']}>
      <Routes>
        <Route path="/knowledge-base" element={<KnowledgeBasePage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('KnowledgeBasePage in Spanish', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCategories.mockResolvedValue([{ category: 'rights', count: 1 }]);
    api.searchKnowledgeBase.mockResolvedValue([entry(1, 'Prior written notice')]);
  });

  afterEach(() => resetTestLanguage());

  it('translates the page chrome (heading, subtitle, search, category tab, disclaimer) while leaving article content in English', async () => {
    await renderPage();

    expect(screen.getByRole('heading', { name: 'Centro de recursos', level: 1 })).toBeInTheDocument();
    expect(
      screen.getByText(
        'Guías en lenguaje sencillo sobre las leyes del IEP, sus derechos y los términos de educación especial'
      )
    ).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Buscar en el centro de recursos...')).toBeInTheDocument();
    expect(await screen.findByTestId('kb-tab-all')).toHaveTextContent('Todos');

    const cards = await screen.findAllByTestId('kb-entry');
    expect(cards[0]).toHaveTextContent('Prior written notice'); // article content stays English
    expect(screen.getByTestId('kb-entry-english-note')).toHaveTextContent('Disponible en inglés');
    // Marked `lang="en"` since the surrounding UI is Spanish — tells
    // assistive tech this text is a different language than the page.
    expect(screen.getByRole('heading', { name: 'Prior written notice' })).toHaveAttribute('lang', 'en');
    expect(screen.getByText('About Prior written notice.')).toHaveAttribute('lang', 'en');

    expect(
      screen.getByText('Esta información se ofrece con fines educativos. No constituye asesoría legal.')
    ).toBeInTheDocument();
  });

  it('translates a known category tab label', async () => {
    await renderPage();

    expect(await screen.findByTestId('kb-tab-rights')).toHaveTextContent('Derechos');
  });
});
