import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const familyContactApi = vi.hoisted(() => ({
  getContactAttempts: vi.fn(),
  getOfflineInput: vi.fn(),
  recordContactAttempt: vi.fn(),
  recordOfflineInput: vi.fn(),
}));
vi.mock('../api/family-contact-api', () => familyContactApi);

const useDocumentListMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/document-authoring/hooks/use-document-list', () => ({
  useDocumentList: useDocumentListMock,
}));

import { FamilyContactCard } from './family-contact-card';

describe('FamilyContactCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useDocumentListMock.mockReturnValue({ documents: [], isLoading: false, error: null, removeDocument: vi.fn() });
    familyContactApi.getContactAttempts.mockResolvedValue({ success: true, data: [] });
    familyContactApi.getOfflineInput.mockResolvedValue({ success: true, data: [] });
  });

  it('shows empty hints for both lists when there is no history', async () => {
    render(<FamilyContactCard studentId={10} />);

    expect(await screen.findByTestId('contact-attempts-empty')).toBeInTheDocument();
    expect(screen.getByTestId('offline-input-empty')).toBeInTheDocument();
  });

  it('logs a contact attempt and shows it newest-first', async () => {
    const user = userEvent.setup();
    familyContactApi.recordContactAttempt.mockResolvedValue({
      success: true,
      data: {
        id: 1,
        schoolStudentId: 10,
        attemptedAt: '2026-09-15T00:00:00.000Z',
        method: 'Phone',
        outcome: 'Reached',
        note: 'Discussed the upcoming meeting.',
        recordedByUserId: 1,
        recordedByName: 'Casey Manager',
      },
    });

    render(<FamilyContactCard studentId={10} />);
    await screen.findByTestId('contact-attempts-empty');

    await user.click(screen.getByTestId('log-contact-attempt-open'));
    await user.selectOptions(screen.getByTestId('contact-attempt-method'), 'Phone');
    await user.selectOptions(screen.getByTestId('contact-attempt-outcome'), 'Reached');
    await user.type(screen.getByTestId('contact-attempt-note'), 'Discussed the upcoming meeting.');
    await user.click(screen.getByTestId('contact-attempt-submit'));

    await waitFor(() => expect(familyContactApi.recordContactAttempt).toHaveBeenCalled());
    expect(await screen.findByTestId('contact-attempt-1')).toHaveTextContent('Discussed the upcoming meeting.');
    expect(screen.queryByTestId('log-contact-attempt-form')).not.toBeInTheDocument();
  });

  it('records offline input and shows it in the list', async () => {
    const user = userEvent.setup();
    familyContactApi.recordOfflineInput.mockResolvedValue({
      success: true,
      data: {
        id: 2,
        schoolStudentId: 10,
        documentInstanceId: null,
        receivedAt: '2026-09-14T00:00:00.000Z',
        method: 'Letter',
        summary: 'Family sent a note about transportation.',
        recordedByUserId: 1,
        recordedByName: 'Casey Manager',
      },
    });

    render(<FamilyContactCard studentId={10} />);
    await screen.findByTestId('offline-input-empty');

    await user.click(screen.getByTestId('record-offline-input-open'));
    await user.type(screen.getByTestId('offline-input-summary'), 'Family sent a note about transportation.');
    await user.click(screen.getByTestId('offline-input-submit'));

    await waitFor(() => expect(familyContactApi.recordOfflineInput).toHaveBeenCalled());
    expect(await screen.findByTestId('offline-input-2')).toHaveTextContent(
      'Family sent a note about transportation.'
    );
  });
});
