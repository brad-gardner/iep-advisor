import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { SharedDraftRevisionDto } from '../types';

const draftSharingApi = vi.hoisted(() => ({
  getShares: vi.fn(),
}));
vi.mock('../api/draft-sharing-api', () => draftSharingApi);

import { SharedBanner } from './shared-banner';

function makeRevision(overrides: Partial<SharedDraftRevisionDto> = {}): SharedDraftRevisionDto {
  return {
    id: 5,
    documentInstanceId: 7,
    studentId: 1,
    studentName: 'Alex Student',
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    revisionNumber: 2,
    status: 'Active',
    sharedAt: '2026-09-10T00:00:00.000Z',
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

describe('SharedBanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing when the draft has never been shared', async () => {
    draftSharingApi.getShares.mockResolvedValue({ success: true, data: [] });
    const { container } = render(<SharedBanner instanceId={7} />);

    await waitFor(() => expect(draftSharingApi.getShares).toHaveBeenCalled());
    expect(container).toBeEmptyDOMElement();
  });

  it('shows the latest active revision and change chips', async () => {
    draftSharingApi.getShares.mockResolvedValue({
      success: true,
      data: [
        makeRevision({
          openResponseCount: 2,
          changeSummary: {
            addedRows: [{ fieldKey: 'f1', fieldLabel: 'Goals', rowId: 'r1', label: 'Reading goal' }],
            removedRows: [],
            changedRows: [],
            changedFields: [],
            summaryText: 'Added a new reading goal.',
          },
        }),
      ],
    });

    render(<SharedBanner instanceId={7} />);

    expect(await screen.findByTestId('shared-banner')).toHaveTextContent('Shared as revision 2');
    expect(screen.getByTestId('shared-banner')).toHaveTextContent('2 open responses');
    expect(screen.getByTestId('shared-banner-changes-added')).toHaveTextContent('1 added');
  });
});
