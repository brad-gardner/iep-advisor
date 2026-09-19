import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
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

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/knowledge-base" element={<KnowledgeBasePage />} />
        <Route path="/knowledge-base/:entryId" element={<KnowledgeBasePage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('KnowledgeBasePage deep link', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getCategories.mockResolvedValue([]);
    api.searchKnowledgeBase.mockResolvedValue([entry(3, 'Prior written notice'), entry(4, 'Evaluations')]);
    Element.prototype.scrollIntoView = vi.fn();
  });

  it('marks and scrolls to the entry named in /knowledge-base/:entryId', async () => {
    renderAt('/knowledge-base/3');
    const cards = await screen.findAllByTestId('kb-entry');
    expect(cards).toHaveLength(2);
    const target = cards.find((c) => c.id === 'kb-entry-3');
    expect(target).toHaveAttribute('data-highlighted', 'true');
    expect(cards.find((c) => c.id === 'kb-entry-4')).not.toHaveAttribute('data-highlighted');
    await waitFor(() => expect(Element.prototype.scrollIntoView).toHaveBeenCalled());
  });

  it('highlights nothing on the plain list', async () => {
    renderAt('/knowledge-base');
    const cards = await screen.findAllByTestId('kb-entry');
    expect(cards.every((c) => !c.hasAttribute('data-highlighted'))).toBe(true);
    expect(Element.prototype.scrollIntoView).not.toHaveBeenCalled();
  });
});
