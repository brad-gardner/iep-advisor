import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import { Drawer } from './drawer';

function renderDrawer(props: Partial<React.ComponentProps<typeof Drawer>> = {}) {
  const onClose = vi.fn();
  const utils = render(
    <Drawer open title="New IEP" onClose={onClose} data-testid="iep-drawer" {...props}>
      <p data-testid="drawer-body">Form fields</p>
    </Drawer>,
  );
  return { onClose, ...utils };
}

describe('Drawer', () => {
  it('renders as a labelled dialog when open', () => {
    renderDrawer();
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAccessibleName('New IEP');
  });

  it('unmounts children when closed', () => {
    const { rerender } = renderDrawer();
    expect(screen.getByTestId('drawer-body')).toBeInTheDocument();
    rerender(
      <Drawer open={false} title="New IEP" onClose={vi.fn()} data-testid="iep-drawer">
        <p data-testid="drawer-body">Form fields</p>
      </Drawer>,
    );
    expect(screen.queryByTestId('drawer-body')).not.toBeInTheDocument();
  });

  it('closes on the header button, a backdrop click, and Esc — once each', () => {
    const { onClose } = renderDrawer();
    fireEvent.click(screen.getByTestId('iep-drawer-close'));
    fireEvent.click(screen.getByRole('dialog'));
    fireEvent(
      screen.getByRole('dialog'),
      new Event('cancel', { bubbles: false, cancelable: true }),
    );
    expect(onClose).toHaveBeenCalledTimes(3);
  });
});
