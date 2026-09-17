import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { MeetingDecisionDto } from '../types';

const decisionsApi = vi.hoisted(() => ({
  getDecisions: vi.fn(),
  createDecision: vi.fn(),
  updateDecision: vi.fn(),
  deleteDecision: vi.fn(),
}));
vi.mock('../api/meeting-decisions-api', () => decisionsApi);

const documentsApi = vi.hoisted(() => ({
  getDocument: vi.fn().mockResolvedValue({ success: false }),
}));
vi.mock('@/features/document-authoring/api/documents-api', () => documentsApi);

import { DecisionsPanel } from './decisions-panel';

function makeDecision(overrides: Partial<MeetingDecisionDto> = {}): MeetingDecisionDto {
  return {
    id: 1,
    meetingId: 100,
    targetFieldKey: null,
    targetRowId: null,
    targetLabel: 'Reading goal',
    text: 'Team agreed to increase the reading fluency target.',
    outcome: 'Agreed',
    recordedByUserId: 1,
    recordedByName: 'Casey Manager',
    createdAt: '2026-09-10T00:00:00.000Z',
    appliedAt: null,
    ...overrides,
  };
}

describe('DecisionsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    documentsApi.getDocument.mockResolvedValue({ success: false });
  });

  it('shows an empty hint when there are no decisions yet', async () => {
    decisionsApi.getDecisions.mockResolvedValue({ success: true, data: [] });
    render(<DecisionsPanel meetingId={100} documentInstanceId={null} canManage />);

    expect(await screen.findByTestId('decisions-empty')).toBeInTheDocument();
  });

  it('adds a free-text-target decision and shows it in the list', async () => {
    const user = userEvent.setup();
    decisionsApi.getDecisions.mockResolvedValue({ success: true, data: [] });
    const created = makeDecision({ id: 5, targetLabel: 'Placement', text: 'Continue in general education.' });
    decisionsApi.createDecision.mockResolvedValue({ success: true, data: created });

    render(<DecisionsPanel meetingId={100} documentInstanceId={null} canManage />);
    await screen.findByTestId('decisions-empty');

    await user.click(screen.getByTestId('decision-add-open'));
    await user.selectOptions(screen.getByTestId('decision-target-select'), '__custom__');
    await user.type(screen.getByTestId('decision-target-custom'), 'Placement');
    await user.type(screen.getByTestId('decision-text'), 'Continue in general education.');
    await user.click(screen.getByTestId('decision-submit'));

    await waitFor(() =>
      expect(decisionsApi.createDecision).toHaveBeenCalledWith(100, {
        text: 'Continue in general education.',
        outcome: 'Agreed',
        targetLabel: 'Placement',
      })
    );
    expect(await screen.findByTestId('decision-5')).toHaveTextContent('Continue in general education.');
    expect(screen.queryByTestId('decision-form')).not.toBeInTheDocument();
  });

  it('deletes a decision via the confirm dialog', async () => {
    const user = userEvent.setup();
    const decision = makeDecision();
    decisionsApi.getDecisions.mockResolvedValue({ success: true, data: [decision] });
    decisionsApi.deleteDecision.mockResolvedValue(undefined);

    render(<DecisionsPanel meetingId={100} documentInstanceId={null} canManage />);
    await screen.findByTestId('decision-1');

    await user.click(screen.getByTestId('decision-delete-1'));
    await user.click(screen.getByTestId('decision-delete-dialog-confirm'));

    await waitFor(() => expect(decisionsApi.deleteDecision).toHaveBeenCalledWith(1));
    expect(screen.queryByTestId('decision-1')).not.toBeInTheDocument();
    expect(await screen.findByTestId('decisions-empty')).toBeInTheDocument();
  });

  it('hides add/edit/delete controls for a non-managing viewer', async () => {
    decisionsApi.getDecisions.mockResolvedValue({ success: true, data: [makeDecision()] });
    render(<DecisionsPanel meetingId={100} documentInstanceId={null} canManage={false} />);

    await screen.findByTestId('decision-1');
    expect(screen.queryByTestId('decision-add-open')).not.toBeInTheDocument();
    expect(screen.queryByTestId('decision-edit-1')).not.toBeInTheDocument();
    expect(screen.queryByTestId('decision-delete-1')).not.toBeInTheDocument();
  });

  it('renders a stored markdown decision text as formatted HTML', async () => {
    decisionsApi.getDecisions.mockResolvedValue({
      success: true,
      data: [makeDecision({ text: 'Team agreed to **increase** the reading fluency target.' })],
    });
    render(<DecisionsPanel meetingId={100} documentInstanceId={null} canManage />);

    await screen.findByTestId('decision-1');
    const strong = screen.getByText('increase');
    expect(strong.tagName).toBe('STRONG');
  });
});
