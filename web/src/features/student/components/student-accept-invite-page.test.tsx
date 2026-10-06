import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { StudentInvitePreviewDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/auth/hooks/use-auth', () => ({ useAuth: useAuthMock }));

const previewInvite = vi.hoisted(() => vi.fn());
vi.mock('../api/student-invite-api', () => ({
  previewInvite,
  acceptInvite: vi.fn(),
}));

import { StudentAcceptInvitePage } from './student-accept-invite-page';

function makePreview(overrides: Partial<StudentInvitePreviewDto> = {}): StudentInvitePreviewDto {
  return {
    inviteSource: 'Parent',
    linkedToFirstName: 'Alex',
    schoolName: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/student/accept-invite?token=abc']}>
      <StudentAcceptInvitePage />
    </MemoryRouter>
  );
}

describe('StudentAcceptInvitePage', () => {
  afterEach(() => {
    vi.clearAllMocks();
    void resetTestLanguage();
  });

  it('renders the inviter context and the linked name as one sentence', async () => {
    useAuthMock.mockReturnValue({ refreshUser: vi.fn() });
    previewInvite.mockResolvedValue({ success: true, data: makePreview() });

    renderPage();

    // The linked name is its own `<span>`, so `.closest('p')` reaches the
    // whole rendered sentence (`getByText`'s default matcher only reads a
    // node's own direct text, not text split across nested elements).
    const name = await screen.findByText('Alex');
    expect(name.closest('p')).toHaveTextContent(
      'Your parent or guardian invited you to join your IEP process as Alex.'
    );
  });

  it('renders the whole sentence in Spanish', async () => {
    useAuthMock.mockReturnValue({ refreshUser: vi.fn() });
    previewInvite.mockResolvedValue({
      success: true,
      data: makePreview({ inviteSource: 'Educator', schoolName: null }),
    });

    await renderInSpanish(
      <MemoryRouter initialEntries={['/student/accept-invite?token=abc']}>
        <StudentAcceptInvitePage />
      </MemoryRouter>
    );

    const name = await screen.findByText('Alex');
    expect(name.closest('p')).toHaveTextContent('Su escuela le invitó a unirse a su proceso de IEP como Alex.');
  });
});
