import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { ChildAccessEntry } from '@/types/api';

const sharingApi = vi.hoisted(() => ({ getAccessList: vi.fn(), revokeAccess: vi.fn() }));
vi.mock('../api/sharing-api', () => sharingApi);

import { AccessList } from './access-list';

function makeEntry(overrides: Partial<ChildAccessEntry> = {}): ChildAccessEntry {
  return {
    id: 1,
    childProfileId: 1,
    userId: 2,
    role: 'viewer',
    userName: 'Pat Co-Parent',
    userEmail: 'pat@example.com',
    inviteEmail: null,
    acceptedAt: null,
    isPending: false,
    createdAt: '2026-01-01T00:00:00.000Z',
    ...overrides,
  };
}

describe('AccessList in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('translates the role badge and the revoke button label', async () => {
    sharingApi.getAccessList.mockResolvedValue({ success: true, data: [makeEntry()] });
    await renderInSpanish(
      <ToastProvider>
        <AccessList childId={1} isOwner />
      </ToastProvider>
    );

    expect(await screen.findByText('Observador')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Revocar el acceso de Pat Co-Parent' })).toBeInTheDocument();
  });

  it('translates the empty state', async () => {
    sharingApi.getAccessList.mockResolvedValue({ success: true, data: [] });
    await renderInSpanish(
      <ToastProvider>
        <AccessList childId={1} isOwner={false} />
      </ToastProvider>
    );

    expect(await screen.findByText('Nadie más tiene acceso a este perfil.')).toBeInTheDocument();
  });
});
