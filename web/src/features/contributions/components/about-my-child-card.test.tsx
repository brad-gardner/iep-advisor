import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const api = vi.hoisted(() => ({
  listContributions: vi.fn(),
  createContribution: vi.fn(),
  updateContribution: vi.fn(),
  deleteContribution: vi.fn(),
}));
vi.mock('../api/contributions-api', async (orig) => ({
  ...(await orig<typeof import('../api/contributions-api')>()),
  ...api,
}));
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: vi.fn() }) }));

import { AboutMyChildCard } from './about-my-child-card';

const note = (id: number, isShared: boolean) => ({
  id, childProfileId: 4, kind: 'WorksAtHome' as const, text: `note ${id}`, isShared, createdAt: '2026-09-01', updatedAt: '2026-09-01',
});

describe('AboutMyChildCard', () => {
  beforeEach(() => {
    api.listContributions.mockResolvedValue({ success: true, data: [note(1, true), note(2, false)] });
    api.createContribution.mockResolvedValue({ success: true, data: note(3, true) });
    api.updateContribution.mockImplementation((id: number, body: { isShared: boolean }) =>
      Promise.resolve({ success: true, data: { ...note(id, body.isShared) } })
    );
  });

  it('labels each note with its visibility so the privacy boundary is on the note itself', async () => {
    render(<AboutMyChildCard childId={4} childName="Jordan" canEdit />);
    await waitFor(() => expect(screen.getByTestId('contribution-1')).toBeInTheDocument());
    expect(screen.getByTestId('contribution-1-visibility')).toHaveTextContent('Visible to the school team');
    expect(screen.getByTestId('contribution-2-visibility')).toHaveTextContent('Private to your family');
  });

  it('adds a note with an explicit share choice and can flip visibility', async () => {
    render(<AboutMyChildCard childId={4} childName="Jordan" canEdit />);
    await waitFor(() => expect(screen.getByTestId('contribution-1')).toBeInTheDocument());

    fireEvent.click(screen.getByTestId('contribution-add'));
    fireEvent.change(screen.getByLabelText('Note'), { target: { value: 'Loves graphic novels' } });
    fireEvent.click(screen.getByTestId('contribution-share'));
    fireEvent.click(screen.getByTestId('contribution-save'));
    await waitFor(() => expect(api.createContribution).toHaveBeenCalledWith(4, { kind: 'Strength', text: 'Loves graphic novels', isShared: true }));
    await waitFor(() => expect(screen.getByTestId('contribution-3')).toBeInTheDocument());

    fireEvent.click(screen.getByTestId('contribution-2-toggle'));
    await waitFor(() => expect(screen.getByTestId('contribution-2-visibility')).toHaveTextContent('Visible to the school team'));
    expect(api.updateContribution).toHaveBeenCalledWith(2, expect.objectContaining({ isShared: true }));
  });

  it('is read-only for viewers', async () => {
    render(<AboutMyChildCard childId={4} childName="Jordan" canEdit={false} />);
    await waitFor(() => expect(screen.getByTestId('contribution-1')).toBeInTheDocument());
    expect(screen.queryByTestId('contribution-add')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contribution-1-toggle')).not.toBeInTheDocument();
  });
});
