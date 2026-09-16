import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { SharedDraftRevisionDto } from '../types';

const sharedDraftsApi = vi.hoisted(() => ({
  getSharedDrafts: vi.fn(),
}));
vi.mock('../api/shared-drafts-api', () => sharedDraftsApi);

import { RevisionSwitcher } from './revision-switcher';

function makeRevision(overrides: Partial<SharedDraftRevisionDto> = {}): SharedDraftRevisionDto {
  return {
    id: 1,
    documentInstanceId: 7,
    studentId: 1,
    studentName: 'Alex Student',
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    revisionNumber: 1,
    status: 'Superseded',
    sharedAt: '2026-08-01T00:00:00.000Z',
    sharedByName: 'Case Manager',
    message: null,
    withdrawnAt: null,
    changeSummary: null,
    acknowledgedAt: null,
    openResponseCount: 0,
    templateVersionId: 1,
    ...overrides,
  };
}

describe('RevisionSwitcher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing for a single-revision document', async () => {
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({ success: true, data: [makeRevision({ id: 2, revisionNumber: 2, status: 'Active' })] });
    const { container } = render(
      <MemoryRouter>
        <RevisionSwitcher childId={1} documentInstanceId={7} currentRevisionId={2} />
      </MemoryRouter>
    );

    await waitFor(() => expect(sharedDraftsApi.getSharedDrafts).toHaveBeenCalled());
    expect(container).toBeEmptyDOMElement();
  });

  it('lists every revision for the document, oldest first, marking the current one', async () => {
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({
      success: true,
      data: [
        makeRevision({ id: 2, revisionNumber: 2, status: 'Active', sharedAt: '2026-09-01T00:00:00.000Z' }),
        makeRevision({ id: 1, revisionNumber: 1, status: 'Superseded' }),
        // A different document instance entirely — must not show up.
        makeRevision({ id: 9, documentInstanceId: 42, revisionNumber: 1 }),
      ],
    });

    render(
      <MemoryRouter>
        <RevisionSwitcher childId={1} documentInstanceId={7} currentRevisionId={2} />
      </MemoryRouter>
    );

    const nav = await screen.findByTestId('revision-switcher');
    expect(nav).toHaveTextContent('Revision 1');
    expect(nav).toHaveTextContent('Revision 2');
    expect(screen.queryByTestId('revision-switcher-item-9')).not.toBeInTheDocument();
    expect(screen.getByTestId('revision-switcher-item-2')).toHaveAttribute('aria-current', 'page');
    expect(screen.getByTestId('revision-switcher-item-1')).not.toHaveAttribute('aria-current');
  });
});
