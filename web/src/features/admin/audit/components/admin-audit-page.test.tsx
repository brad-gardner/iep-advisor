import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { AuditIntegrityRunDto } from '../types';

const api = vi.hoisted(() => ({
  listAuditIntegrityRuns: vi.fn(),
  runAuditIntegrityCheck: vi.fn(),
}));
vi.mock('../api/audit-admin-api', () => api);

import { AdminAuditPage } from './admin-audit-page';

function run(overrides: Partial<AuditIntegrityRunDto> = {}): AuditIntegrityRunDto {
  return {
    id: 1,
    startedAt: '2026-09-16T03:00:00.000Z',
    completedAt: '2026-09-16T03:00:05.000Z',
    rowsChecked: 12000,
    firstBrokenId: null,
    status: 'Ok',
    detail: null,
    ...overrides,
  };
}

describe('AdminAuditPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('lists the recent runs with a status badge', async () => {
    api.listAuditIntegrityRuns.mockResolvedValue({ success: true, data: [run()] });
    render(<AdminAuditPage />);

    await screen.findByTestId('audit-run-status-1');
    expect(screen.getByTestId('audit-run-status-1')).toHaveTextContent('Ok');
    expect(screen.getByText('12000')).toBeInTheDocument();
  });

  it('shows a Broken run with its first broken id', async () => {
    api.listAuditIntegrityRuns.mockResolvedValue({
      success: true,
      data: [run({ status: 'Broken', firstBrokenId: 4821, detail: 'Hash mismatch at row 4821' })],
    });
    render(<AdminAuditPage />);

    expect(await screen.findByTestId('audit-run-status-1')).toHaveTextContent('Broken');
    expect(screen.getByText('4821')).toBeInTheDocument();
    expect(screen.getByText('Hash mismatch at row 4821')).toBeInTheDocument();
  });

  it('runs a check now and refreshes the list', async () => {
    const user = userEvent.setup();
    api.listAuditIntegrityRuns
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValueOnce({ success: true, data: [run()] });
    api.runAuditIntegrityCheck.mockResolvedValue({ success: true, data: run() });

    render(<AdminAuditPage />);
    await waitFor(() => expect(api.listAuditIntegrityRuns).toHaveBeenCalledTimes(1));
    expect(screen.getByText('No integrity runs yet')).toBeInTheDocument();

    await user.click(screen.getByTestId('run-audit-check'));

    await waitFor(() => expect(api.runAuditIntegrityCheck).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(api.listAuditIntegrityRuns).toHaveBeenCalledTimes(2));
    expect(await screen.findByTestId('audit-run-status-1')).toHaveTextContent('Ok');
  });

  it('shows the run error inline without clearing the existing list', async () => {
    const user = userEvent.setup();
    api.listAuditIntegrityRuns.mockResolvedValue({ success: true, data: [run()] });
    api.runAuditIntegrityCheck.mockResolvedValue({ success: false, message: 'Could not run the integrity check.' });

    render(<AdminAuditPage />);
    await screen.findByTestId('audit-run-status-1');

    await user.click(screen.getByTestId('run-audit-check'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not run the integrity check.');
    expect(screen.getByTestId('audit-run-status-1')).toBeInTheDocument();
  });
});
