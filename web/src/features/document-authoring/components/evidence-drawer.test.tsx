import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { EvidenceDrawer } from './evidence-drawer';
import type { StudentEvidenceBundle } from '../api/evidence-api';

const getStudentEvidence = vi.fn();
vi.mock('../api/evidence-api', async (orig) => ({
  ...(await orig<typeof import('../api/evidence-api')>()),
  getStudentEvidence: (...args: unknown[]) => getStudentEvidence(...args),
}));

const bundle: StudentEvidenceBundle = {
  schoolStudentId: 21,
  sources: [],
  items: [
    { id: 'E1', kind: 'Identity', sourceType: 'SchoolStudent', sourceId: 21, sourceLabel: 'Student record', sourceDate: null, authorRole: 'school', text: 'Jordan Ellis, Grade 7', rowId: null, fields: null },
    { id: 'E2', kind: 'PriorGoal', sourceType: 'AuthoredDocumentVersion', sourceId: 1, sourceLabel: 'IEP v1', sourceDate: '2025-10-14T00:00:00Z', authorRole: 'school', text: 'Read 90 wpm', rowId: 'r1', fields: null },
    { id: 'E3', kind: 'ParentContribution', sourceType: 'ParentContribution', sourceId: 5, sourceLabel: 'Family — Strength', sourceDate: null, authorRole: 'family', text: 'Reads aloud to his sister', rowId: null, fields: null },
  ],
};

describe('EvidenceDrawer', () => {
  beforeEach(() => {
    getStudentEvidence.mockReset();
  });

  it('groups items by source, offers Insert only for non-identity items, and explains how to pick a target', async () => {
    getStudentEvidence.mockResolvedValue({ success: true, data: bundle });
    render(<EvidenceDrawer open onClose={() => {}} studentId={21} activeField={null} />);

    expect(await screen.findByRole('region', { name: 'Student & team' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Prior plan' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'From the family' })).toBeInTheDocument();
    expect(screen.queryByTestId('evidence-E1-insert')).not.toBeInTheDocument();

    const hint = screen.getByTestId('evidence-target');
    expect(hint).toHaveTextContent('Close this panel and click into a field');
    const insert = screen.getByTestId('evidence-E2-insert');
    expect(insert).toHaveAttribute('aria-disabled', 'true');
    expect(insert).toHaveAttribute('aria-describedby', hint.id);
    fireEvent.click(insert); // nothing to write into — no crash, no call
  });

  it('inserts into the focused field with a distinct accessible name per item', async () => {
    getStudentEvidence.mockResolvedValue({ success: true, data: bundle });
    const apply = vi.fn();
    render(
      <EvidenceDrawer open onClose={() => {}} studentId={21} activeField={{ id: 'f1', label: () => 'Present Levels', apply }} />
    );

    expect(await screen.findByText(/Inserting into:/)).toBeInTheDocument();
    const insert = screen.getByRole('button', { name: 'Insert IEP v1 into Present Levels' });
    expect(insert).toHaveAttribute('aria-disabled', 'false');
    fireEvent.click(insert);
    expect(apply).toHaveBeenCalledWith('Read 90 wpm');
    expect(screen.getByRole('button', { name: 'Insert Family — Strength into Present Levels' })).toBeInTheDocument();
  });

  it('shows the error and retries when reopened', async () => {
    getStudentEvidence.mockResolvedValueOnce({ success: false, message: 'boom' }).mockResolvedValueOnce({ success: true, data: bundle });
    const { rerender } = render(<EvidenceDrawer open onClose={() => {}} studentId={21} activeField={null} />);
    expect(await screen.findByText('boom')).toBeInTheDocument();

    rerender(<EvidenceDrawer open={false} onClose={() => {}} studentId={21} activeField={null} />);
    rerender(<EvidenceDrawer open onClose={() => {}} studentId={21} activeField={null} />);
    expect(await screen.findByRole('region', { name: 'Prior plan' })).toBeInTheDocument();
    expect(getStudentEvidence).toHaveBeenCalledTimes(2);
  });
});
