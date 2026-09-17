import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const api = vi.hoisted(() => ({ createIep: vi.fn() }));
vi.mock('../api/iep-documents-api', async (orig) => ({
  ...(await orig<typeof import('../api/iep-documents-api')>()),
  ...api,
}));

import { CreateIepForm } from './create-iep-form';

describe('CreateIepForm', () => {
  beforeEach(() => {
    api.createIep.mockReset();
    api.createIep.mockResolvedValue({ success: true, data: {} });
  });

  it('submits attendees and notes as the markdown the rich text editors emit', async () => {
    const onCreated = vi.fn();
    render(<CreateIepForm childId={4} onCreated={onCreated} onCancel={() => {}} />);

    fireEvent.change(screen.getByTestId('iep-meeting-date'), { target: { value: '2026-09-01' } });
    fireEvent.change(screen.getByTestId('iep-meeting-type'), { target: { value: 'annual_review' } });
    fireEvent.change(screen.getByTestId('iep-attendees'), { target: { value: '- Teacher\n- Parent' } });
    fireEvent.change(screen.getByTestId('iep-notes'), { target: { value: 'Discussed **goals** for the year.' } });

    fireEvent.click(screen.getByTestId('iep-create-submit'));

    await waitFor(() =>
      expect(api.createIep).toHaveBeenCalledWith(4, {
        iepDate: '2026-09-01',
        meetingType: 'annual_review',
        attendees: '- Teacher\n- Parent',
        notes: 'Discussed **goals** for the year.',
      })
    );
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });

  it('omits attendees/notes entirely when the editors are left empty', async () => {
    render(<CreateIepForm childId={4} onCreated={() => {}} onCancel={() => {}} />);
    fireEvent.change(screen.getByTestId('iep-meeting-date'), { target: { value: '2026-09-01' } });
    fireEvent.change(screen.getByTestId('iep-meeting-type'), { target: { value: 'initial' } });

    fireEvent.click(screen.getByTestId('iep-create-submit'));

    await waitFor(() =>
      expect(api.createIep).toHaveBeenCalledWith(4, {
        iepDate: '2026-09-01',
        meetingType: 'initial',
        attendees: undefined,
        notes: undefined,
      })
    );
  });
});
