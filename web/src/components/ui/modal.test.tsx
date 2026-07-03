import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import { Modal } from './modal';

function renderModal(props: Partial<React.ComponentProps<typeof Modal>> = {}) {
  const onClose = vi.fn();
  const utils = render(
    <Modal open title="Add school" onClose={onClose} data-testid="school-modal" {...props}>
      <p data-testid="modal-body">Body content</p>
    </Modal>,
  );
  return { onClose, ...utils };
}

describe('Modal', () => {
  it('renders as a labelled dialog when open', () => {
    renderModal();
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    // aria-labelledby points at the heading that renders the title.
    expect(dialog).toHaveAccessibleName('Add school');
    expect(screen.getByRole('heading', { name: 'Add school' })).toBeInTheDocument();
  });

  it('unmounts children when closed', () => {
    const { rerender } = renderModal();
    expect(screen.getByTestId('modal-body')).toBeInTheDocument();
    rerender(
      <Modal open={false} title="Add school" onClose={vi.fn()} data-testid="school-modal">
        <p data-testid="modal-body">Body content</p>
      </Modal>,
    );
    expect(screen.queryByTestId('modal-body')).not.toBeInTheDocument();
  });

  it('calls onClose once when the header close button is clicked', () => {
    const { onClose } = renderModal();
    fireEvent.click(screen.getByTestId('school-modal-close'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes on a true backdrop click but not on content clicks', () => {
    const { onClose } = renderModal();
    fireEvent.click(screen.getByTestId('modal-body'));
    expect(onClose).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('dialog'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('forwards the native cancel (Esc) event as a single close request', () => {
    const { onClose } = renderModal();
    fireEvent(
      screen.getByRole('dialog'),
      new Event('cancel', { bubbles: false, cancelable: true }),
    );
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('hides the close button and renders described-by text in alertdialog mode', () => {
    render(
      <Modal
        open
        role="alertdialog"
        hideCloseButton
        describedById="desc-1"
        title="Delete report"
        onClose={vi.fn()}
        data-testid="confirm"
      >
        <p id="desc-1">This cannot be undone.</p>
      </Modal>,
    );
    expect(screen.getByRole('alertdialog')).toHaveAccessibleDescription('This cannot be undone.');
    expect(screen.queryByTestId('confirm-close')).not.toBeInTheDocument();
  });
});
