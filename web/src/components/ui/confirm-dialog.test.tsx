import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import { ConfirmDialog } from './confirm-dialog';

function renderConfirm(props: Partial<React.ComponentProps<typeof ConfirmDialog>> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  render(
    <ConfirmDialog
      open
      title="Revoke access"
      message="They will immediately lose access to this child."
      confirmLabel="Revoke access"
      onConfirm={onConfirm}
      onCancel={onCancel}
      data-testid="revoke-confirm"
      {...props}
    />,
  );
  return { onConfirm, onCancel };
}

describe('ConfirmDialog', () => {
  it('is an alertdialog described by its consequence text', () => {
    renderConfirm();
    const dialog = screen.getByRole('alertdialog');
    expect(dialog).toHaveAccessibleName('Revoke access');
    expect(dialog).toHaveAccessibleDescription(
      'They will immediately lose access to this child.',
    );
  });

  it('gives initial focus to Cancel, not the destructive action', () => {
    renderConfirm();
    expect(screen.getByTestId('revoke-confirm-cancel')).toHaveFocus();
  });

  it('names the destructive action on its button', () => {
    renderConfirm();
    expect(
      screen.getByRole('button', { name: 'Revoke access' }),
    ).toHaveAttribute('data-testid', 'revoke-confirm-confirm');
  });

  it('routes confirm and cancel to the right callbacks', () => {
    const { onConfirm, onCancel } = renderConfirm();
    fireEvent.click(screen.getByTestId('revoke-confirm-confirm'));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByTestId('revoke-confirm-cancel'));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('disables actions while loading', () => {
    renderConfirm({ loading: true });
    expect(screen.getByTestId('revoke-confirm-cancel')).toBeDisabled();
    expect(screen.getByTestId('revoke-confirm-confirm')).toHaveAttribute('aria-busy', 'true');
  });
});
