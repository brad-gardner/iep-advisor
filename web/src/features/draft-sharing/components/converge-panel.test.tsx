import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';
import type { ConvergeDto, DraftResponseDto } from '../types';

const draftSharingApi = vi.hoisted(() => ({
  getConverge: vi.fn(),
  resolveResponse: vi.fn(),
  getSharePreview: vi.fn().mockResolvedValue({ success: true, data: { recipients: [], policyEnabled: false, lastSharedAt: null, willSupersedeRevision: null } }),
}));
vi.mock('../api/draft-sharing-api', () => draftSharingApi);

import { ConvergePanel } from './converge-panel';

const templateVersion: TemplateVersionDetailDto = {
  id: 1,
  documentTemplateId: 1,
  versionNumber: 1,
  status: 'Published',
  publishedAt: '2026-01-01T00:00:00.000Z',
  rowVersion: null,
  sections: [],
};

function makeResponse(overrides: Partial<DraftResponseDto> = {}): DraftResponseDto {
  return {
    id: 9,
    revisionId: 3,
    parentUserId: 1,
    parentName: 'Jamie Parent',
    targetFieldKey: null,
    targetRowId: null,
    targetLabel: 'Reading goal',
    kind: 'Question',
    text: 'Is this ambitious enough?',
    createdAt: '2026-09-10T00:00:00.000Z',
    status: 'Open',
    staffReply: null,
    resolvedInDraft: false,
    resolvedByName: null,
    resolvedAt: null,
    ...overrides,
  };
}

function makeConverge(overrides: Partial<ConvergeDto> = {}): ConvergeDto {
  return {
    instanceId: 7,
    latestRevision: null,
    openResponses: [],
    resolvedResponses: [],
    changesSinceShare: null,
    acknowledgements: [],
    canShare: true,
    policyEnabled: false,
    ...overrides,
  };
}

describe('ConvergePanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    draftSharingApi.getSharePreview.mockResolvedValue({
      success: true,
      data: { recipients: [], policyEnabled: false, lastSharedAt: null, willSupersedeRevision: null },
    });
  });

  it('lists open responses and requires a reply or resolved-in-draft to resolve', async () => {
    const user = userEvent.setup();
    draftSharingApi.getConverge.mockResolvedValue({ success: true, data: makeConverge({ openResponses: [makeResponse()] }) });

    render(
      <ToastProvider>
        <ConvergePanel instanceId={7} status="Draft" templateVersion={templateVersion} />
      </ToastProvider>
    );

    expect(await screen.findByTestId('response-card-9')).toHaveTextContent('Is this ambitious enough?');
    await user.click(screen.getByTestId('response-resolve-open-9'));

    expect(await screen.findByTestId('resolve-response-dialog')).toBeInTheDocument();
    expect(screen.getByTestId('resolve-response-submit')).toBeDisabled();

    await user.type(screen.getByTestId('resolve-reply-input'), 'Yes, we increased the target.');
    expect(screen.getByTestId('resolve-response-submit')).not.toBeDisabled();
  });

  it('resolves a response and moves it to the resolved list', async () => {
    const user = userEvent.setup();
    draftSharingApi.getConverge.mockResolvedValue({ success: true, data: makeConverge({ openResponses: [makeResponse()] }) });
    const resolved = makeResponse({ status: 'Resolved', staffReply: 'Yes, updated.', resolvedByName: 'Case Manager' });
    draftSharingApi.resolveResponse.mockResolvedValue({ success: true, data: resolved });

    render(
      <ToastProvider>
        <ConvergePanel instanceId={7} status="Draft" templateVersion={templateVersion} />
      </ToastProvider>
    );

    await user.click(await screen.findByTestId('response-resolve-open-9'));
    await user.type(screen.getByTestId('resolve-reply-input'), 'Yes, updated.');
    await user.click(screen.getByTestId('resolve-response-submit'));

    await waitFor(() =>
      expect(draftSharingApi.resolveResponse).toHaveBeenCalledWith(9, { staffReply: 'Yes, updated.', resolvedInDraft: false })
    );
    expect(await screen.findByTestId('converge-resolved-responses')).toHaveTextContent('Yes, updated.');
    expect(screen.queryByTestId('converge-open-responses')).not.toBeInTheDocument();
  });

  it('surfaces a server refusal from a failed resolve inline', async () => {
    const user = userEvent.setup();
    draftSharingApi.getConverge.mockResolvedValue({ success: true, data: makeConverge({ openResponses: [makeResponse()] }) });
    draftSharingApi.resolveResponse.mockRejectedValue(apiRejection('This response was already resolved.'));

    render(
      <ToastProvider>
        <ConvergePanel instanceId={7} status="Draft" templateVersion={templateVersion} />
      </ToastProvider>
    );

    await user.click(await screen.findByTestId('response-resolve-open-9'));
    await user.type(screen.getByTestId('resolve-reply-input'), 'A reply');
    await user.click(screen.getByTestId('resolve-response-submit'));

    expect(await screen.findByRole('alert')).toHaveTextContent('This response was already resolved.');
  });
});
