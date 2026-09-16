import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { makeStudent } from '../../test/fixtures';
import { StudentLifecycleActions } from './student-lifecycle-actions';

const schools = [
  { id: 5, name: 'Lincoln Elementary', activeStudentCount: 0, activeStaffCount: 0 },
  { id: 6, name: 'Roosevelt Middle', activeStudentCount: 0, activeStaffCount: 0 },
];

function renderActions(props: Partial<React.ComponentProps<typeof StudentLifecycleActions>> = {}) {
  const handlers = {
    onExit: vi.fn().mockResolvedValue({ success: true }),
    onReactivate: vi.fn().mockResolvedValue({ success: true }),
    onArchive: vi.fn().mockResolvedValue({ success: true }),
    onTransfer: vi.fn().mockResolvedValue({ success: true }),
  };
  render(<StudentLifecycleActions student={makeStudent()} {...handlers} {...props} />);
  return handlers;
}

describe('StudentLifecycleActions', () => {
  it('exits with a reason and optional date', async () => {
    const user = userEvent.setup();
    const { onExit } = renderActions();
    await user.click(screen.getByRole('button', { name: 'Actions for Ada Lovelace' }));
    await user.click(screen.getByTestId('student-action-exit'));
    await user.selectOptions(screen.getByLabelText('Reason *'), 'Transferred');
    await user.type(screen.getByLabelText('Exit date'), '2026-06-01');
    await user.click(screen.getByTestId('exit-student-submit'));
    await waitFor(() =>
      expect(onExit).toHaveBeenCalledWith({ exitReason: 'Transferred', exitedAt: '2026-06-01' })
    );
  });

  it('offers Transfer only when schools are supplied (DistrictAdmin) and excludes the current school', async () => {
    const user = userEvent.setup();
    const { onTransfer } = renderActions({ schools });
    await user.click(screen.getByRole('button', { name: 'Actions for Ada Lovelace' }));
    await user.click(screen.getByTestId('student-action-transfer'));
    const picker = screen.getByLabelText('New school *');
    expect(screen.queryByRole('option', { name: 'Lincoln Elementary' })).not.toBeInTheDocument();
    await user.selectOptions(picker, '6');
    await user.click(screen.getByTestId('transfer-student-submit'));
    await waitFor(() => expect(onTransfer).toHaveBeenCalledWith({ newSchoolId: 6 }));
  });

  it('shows Reactivate instead of Exit for a non-active student and hides Transfer without schools', async () => {
    const user = userEvent.setup();
    const { onReactivate } = renderActions({ student: makeStudent({ status: 'Exited', isActive: false }) });
    await user.click(screen.getByRole('button', { name: 'Actions for Ada Lovelace' }));
    expect(screen.queryByTestId('student-action-exit')).not.toBeInTheDocument();
    expect(screen.queryByTestId('student-action-transfer')).not.toBeInTheDocument();
    await user.click(screen.getByTestId('student-action-reactivate'));
    await user.click(screen.getByTestId('reactivate-student-dialog-confirm'));
    await waitFor(() => expect(onReactivate).toHaveBeenCalled());
  });
});
