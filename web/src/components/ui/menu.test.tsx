import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import { Menu } from './menu';

function renderMenu(onEdit = vi.fn(), onDelete = vi.fn()) {
  render(
    <Menu
      label="Actions for Lincoln Elementary"
      data-testid="school-actions"
      items={[
        { label: 'Edit', onSelect: onEdit, 'data-testid': 'menu-edit' },
        { label: 'Deactivate', onSelect: onDelete, variant: 'danger', 'data-testid': 'menu-deactivate' },
      ]}
    />,
  );
  return { onEdit, onDelete };
}

describe('Menu', () => {
  it('exposes a labelled menu-button trigger that is collapsed by default', () => {
    renderMenu();
    const trigger = screen.getByRole('button', { name: 'Actions for Lincoln Elementary' });
    expect(trigger).toHaveAttribute('aria-haspopup', 'menu');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('opens on click and focuses the first item', () => {
    renderMenu();
    fireEvent.click(screen.getByTestId('school-actions'));
    expect(screen.getByRole('menu')).toBeInTheDocument();
    expect(screen.getByTestId('school-actions')).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByTestId('menu-edit')).toHaveFocus();
  });

  it('moves focus with ArrowDown and wraps', () => {
    renderMenu();
    fireEvent.click(screen.getByTestId('school-actions'));
    const menu = screen.getByRole('menu');
    fireEvent.keyDown(menu, { key: 'ArrowDown' });
    expect(screen.getByTestId('menu-deactivate')).toHaveFocus();
    fireEvent.keyDown(menu, { key: 'ArrowDown' });
    expect(screen.getByTestId('menu-edit')).toHaveFocus();
  });

  it('closes on Escape and returns focus to the trigger', () => {
    renderMenu();
    const trigger = screen.getByTestId('school-actions');
    fireEvent.click(trigger);
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'Escape' });
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it('skips disabled items during roving focus', () => {
    const onA = vi.fn();
    const onC = vi.fn();
    render(
      <Menu
        label="Actions"
        data-testid="actions"
        items={[
          { label: 'A', onSelect: onA, 'data-testid': 'item-a' },
          { label: 'B', onSelect: vi.fn(), disabled: true, 'data-testid': 'item-b' },
          { label: 'C', onSelect: onC, 'data-testid': 'item-c' },
        ]}
      />,
    );
    fireEvent.click(screen.getByTestId('actions'));
    // Opens on the first enabled item.
    expect(screen.getByTestId('item-a')).toHaveFocus();
    // ArrowDown jumps over the disabled B straight to C.
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'ArrowDown' });
    expect(screen.getByTestId('item-c')).toHaveFocus();
    // Wraps back to A, still skipping B.
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'ArrowDown' });
    expect(screen.getByTestId('item-a')).toHaveFocus();
  });

  it('selecting an item runs its action and closes the menu', () => {
    const { onDelete } = renderMenu();
    fireEvent.click(screen.getByTestId('school-actions'));
    fireEvent.click(screen.getByTestId('menu-deactivate'));
    expect(onDelete).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });
});
