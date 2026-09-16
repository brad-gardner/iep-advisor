import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import type { RecipientPreviewDto } from '../types';

const draftSharingApi = vi.hoisted(() => ({
  getSharePreview: vi.fn(),
  shareDraft: vi.fn(),
}));
vi.mock('../api/draft-sharing-api', () => draftSharingApi);

import { ShareWithFamilyButton } from './share-with-family-button';

function makePreview(overrides: Partial<RecipientPreviewDto> = {}): RecipientPreviewDto {
  return {
    recipients: [{ userId: 1, displayName: 'Jamie Parent', relationship: 'Parent', email: 'jamie@example.com' }],
    policyEnabled: true,
    lastSharedAt: null,
    willSupersedeRevision: null,
    ...overrides,
  };
}

function renderButton(props: Partial<React.ComponentProps<typeof ShareWithFamilyButton>> = {}) {
  const onShared = vi.fn();
  render(
    <ToastProvider>
      <ShareWithFamilyButton instanceId={7} status="Draft" onShared={onShared} {...props} />
    </ToastProvider>
  );
  return { onShared };
}

describe('ShareWithFamilyButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('hides entirely when the district has disabled family draft sharing', async () => {
    draftSharingApi.getSharePreview.mockResolvedValue({ success: true, data: makePreview({ policyEnabled: false }) });
    renderButton();

    await waitFor(() => expect(draftSharingApi.getSharePreview).toHaveBeenCalled());
    expect(screen.queryByTestId('share-with-family-open')).not.toBeInTheDocument();
  });

  it('hides for a Finalized document (not Draft/Finalizing)', async () => {
    draftSharingApi.getSharePreview.mockResolvedValue({ success: true, data: makePreview() });
    renderButton({ status: 'Finalized' });

    await waitFor(() => expect(draftSharingApi.getSharePreview).toHaveBeenCalled());
    expect(screen.queryByTestId('share-with-family-open')).not.toBeInTheDocument();
  });

  it('shows the recipient preview and shares the draft', async () => {
    const user = userEvent.setup();
    draftSharingApi.getSharePreview.mockResolvedValue({ success: true, data: makePreview() });
    draftSharingApi.shareDraft.mockResolvedValue({
      success: true,
      data: { id: 1, revisionNumber: 1, documentInstanceId: 7, status: 'Active' },
    });
    const { onShared } = renderButton();

    const openButton = await screen.findByTestId('share-with-family-open');
    await user.click(openButton);

    expect(await screen.findByTestId('share-recipient-list')).toHaveTextContent('Jamie Parent');
    expect(screen.getByTestId('share-recipient-list')).toHaveTextContent('Parent');
    expect(screen.getByText('jamie@example.com')).toBeInTheDocument();

    await user.type(screen.getByTestId('share-message-input'), 'Please review before Thursday');
    await user.click(screen.getByTestId('share-with-family-submit'));

    await waitFor(() =>
      expect(draftSharingApi.shareDraft).toHaveBeenCalledWith(7, { message: 'Please review before Thursday' })
    );
    await waitFor(() => expect(onShared).toHaveBeenCalled());
  });
});
