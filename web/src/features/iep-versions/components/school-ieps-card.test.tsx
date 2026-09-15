import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const legacyMock = vi.fn();
const authoredMock = vi.fn();
vi.mock('../hooks/use-version-list', () => ({ useChildVersions: () => legacyMock() }));
vi.mock('@/features/document-authoring/hooks/use-child-authored-versions', () => ({
  useChildAuthoredVersions: () => authoredMock(),
}));

import { SchoolIepsCard } from './school-ieps-card';

function renderCard() {
  return render(
    <MemoryRouter>
      <SchoolIepsCard childId={4} />
    </MemoryRouter>
  );
}

describe('SchoolIepsCard', () => {
  it('renders nothing while either source is loading (no empty-card flash)', () => {
    legacyMock.mockReturnValue({ versions: [], isLoading: true });
    authoredMock.mockReturnValue({ versions: [], isLoading: false });
    const { container } = renderCard();
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when both sources are empty', () => {
    legacyMock.mockReturnValue({ versions: [], isLoading: false });
    authoredMock.mockReturnValue({ versions: [], isLoading: false });
    const { container } = renderCard();
    expect(container).toBeEmptyDOMElement();
  });

  it('merges template and legacy versions into one list, newest first, with the right links', () => {
    legacyMock.mockReturnValue({
      versions: [{ id: 1, title: null, versionNumber: 1, finalizedAt: '2025-10-01T00:00:00Z', pdfRenderStatus: 'Rendered' }],
      isLoading: false,
    });
    authoredMock.mockReturnValue({
      versions: [
        { id: 9, documentTypeDisplayName: 'ETR', versionNumber: 1, finalizedAt: '2026-01-15T00:00:00Z', pdfRenderStatus: 'Pending' },
        { id: 7, documentTypeDisplayName: 'IEP', versionNumber: 2, finalizedAt: '2026-03-01T00:00:00Z', pdfRenderStatus: 'Rendered' },
      ],
      isLoading: false,
    });
    renderCard();
    const links = screen.getAllByRole('link');
    expect(links.map((l) => l.textContent?.split('Finalized')[0].trim())).toEqual(['IEP v2', 'ETR v1', 'IEP v1']);
    expect(links[0]).toHaveAttribute('href', '/children/4/authored-versions/7');
    expect(links[2]).toHaveAttribute('href', '/children/4/iep-versions/1');
  });
});
