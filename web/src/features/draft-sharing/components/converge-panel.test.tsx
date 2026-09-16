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
  shareDraft: vi.fn(),
  getSharePreview: vi.fn().mockResolvedValue({ success: true, data: { recipients: [], policyEnabled: false, lastSharedAt: null, willSupersedeRevision: null } }),
}));
vi.mock('../api/draft-sharing-api', () => draftSharingApi);

import { ConvergePanel } from './converge-panel';
import { fieldElementId } from '@/features/document-authoring/components/field-renderers/types';

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

  it('reveals the editor before jumping to a response\'s field (the editor is hidden behind this tab)', async () => {
    const user = userEvent.setup();
    const withField: TemplateVersionDetailDto = {
      ...templateVersion,
      sections: [
        {
          id: 1,
          sectionKey: 'goals',
          title: 'Goals',
          displayOrder: 0,
          fields: [{ id: 100, fieldKey: 'goals-field', fieldType: 'Text', label: 'Goal', required: false, displayOrder: 0, configJson: null }],
        },
      ],
    };
    draftSharingApi.getConverge.mockResolvedValue({
      success: true,
      data: makeConverge({ openResponses: [makeResponse({ targetFieldKey: 'goals-field' })] }),
    });
    const onBeforeJump = vi.fn();
    const raf = vi.spyOn(window, 'requestAnimationFrame').mockImplementation((cb) => {
      cb(0);
      return 0;
    });
    const scrollIntoView = vi.fn();
    const field = document.createElement('input');
    field.id = fieldElementId(100);
    field.scrollIntoView = scrollIntoView;
    document.body.appendChild(field);

    render(
      <ToastProvider>
        <ConvergePanel instanceId={7} status="Draft" templateVersion={withField} onBeforeJump={onBeforeJump} />
      </ToastProvider>
    );

    await user.click(await screen.findByTestId('response-jump-9'));
    expect(onBeforeJump).toHaveBeenCalledTimes(1);
    expect(scrollIntoView).toHaveBeenCalled();
    expect(document.activeElement).toBe(field);
    // The reveal happens before the (next-frame) scroll.
    expect(onBeforeJump.mock.invocationCallOrder[0]).toBeLessThan(scrollIntoView.mock.invocationCallOrder[0]);

    raf.mockRestore();
    field.remove();
  });

  it('keeps the loaded responses and an open resolve dialog when a refresh fails', async () => {
    const user = userEvent.setup();
    draftSharingApi.getConverge.mockResolvedValueOnce({ success: true, data: makeConverge({ openResponses: [makeResponse()] }) });
    draftSharingApi.getSharePreview.mockResolvedValue({
      success: true,
      data: { recipients: [{ userId: 1, displayName: 'Jamie Parent', relationship: 'Parent', email: 'j@example.com' }], policyEnabled: true, lastSharedAt: null, willSupersedeRevision: null },
    });

    render(
      <ToastProvider>
        <ConvergePanel instanceId={7} status="Draft" templateVersion={templateVersion} />
      </ToastProvider>
    );

    await user.click(await screen.findByTestId('response-resolve-open-9'));
    await user.type(screen.getByTestId('resolve-reply-input'), 'Half-typed reply');

    // A refresh (what "Share again" triggers) that fails must not tear the tree down.
    draftSharingApi.shareDraft.mockResolvedValue({ success: true, data: { id: 3, revisionNumber: 1 } });
    draftSharingApi.getConverge.mockRejectedValueOnce(apiRejection('Server error', 500));
    await user.click(screen.getByTestId('share-with-family-open'));
    await user.click(await screen.findByTestId('share-with-family-submit'));

    expect(await screen.findByTestId('converge-retry')).toBeInTheDocument();
    expect(screen.getByTestId('converge-open-responses')).toBeInTheDocument();
    expect(screen.getByTestId('resolve-reply-input')).toHaveValue('Half-typed reply');
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
