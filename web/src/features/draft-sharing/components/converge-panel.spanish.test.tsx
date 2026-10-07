import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';
import type { ConvergeDto, DraftResponseDto } from '../types';

const draftSharingApi = vi.hoisted(() => ({
  getConverge: vi.fn(),
  resolveResponse: vi.fn(),
  shareDraft: vi.fn(),
  getSharePreview: vi.fn(),
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

function renderPanel() {
  return renderInSpanish(
    <ToastProvider>
      <ConvergePanel instanceId={7} status="Draft" templateVersion={templateVersion} />
    </ToastProvider>
  );
}

describe('ConvergePanel in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the open-responses heading and the response kind in Spanish', async () => {
    vi.clearAllMocks();
    draftSharingApi.getSharePreview.mockResolvedValue({
      success: true,
      data: { recipients: [], policyEnabled: false, lastSharedAt: null, willSupersedeRevision: null },
    });
    draftSharingApi.getConverge.mockResolvedValue({ success: true, data: makeConverge({ openResponses: [makeResponse()] }) });

    await renderPanel();

    expect(await screen.findByRole('heading', { name: 'Respuestas abiertas' })).toBeInTheDocument();
    const card = screen.getByTestId('response-card-9');
    expect(card).toHaveTextContent('Pregunta');
    expect(card).toHaveTextContent('Is this ambitious enough?');
  });

  it("opens the resolve dialog in Spanish, with the response kind translated in the quoted prefix", async () => {
    const user = userEvent.setup();
    vi.clearAllMocks();
    draftSharingApi.getSharePreview.mockResolvedValue({
      success: true,
      data: { recipients: [], policyEnabled: false, lastSharedAt: null, willSupersedeRevision: null },
    });
    draftSharingApi.getConverge.mockResolvedValue({ success: true, data: makeConverge({ openResponses: [makeResponse()] }) });

    await renderPanel();

    await user.click(await screen.findByTestId('response-resolve-open-9'));

    const dialog = await screen.findByTestId('resolve-response-dialog');
    expect(dialog).toHaveTextContent('Responder y resolver');
    expect(dialog).toHaveTextContent('Jamie Parent');
    expect(dialog).toHaveTextContent('Pregunta');
    expect(screen.getByRole('button', { name: 'Resolver' })).toBeInTheDocument();
  });
});
