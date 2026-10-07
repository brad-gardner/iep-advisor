import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `@/app/lazy-routes/staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff and
// admin namespaces". This component renders directly here (not through the
// lazy route), so its English must be registered the same way the real
// route chunk does.
import '@/app/lazy-routes/staff-locales';
import { ObjectivesEditor } from './objectives-editor';

function objectivesOf(...items: Array<{ _rowId?: string; description: string; criteria?: string; targetDate?: string }>) {
  return items.map((i) => ({
    _rowId: i._rowId,
    description: i.description,
    criteria: i.criteria ?? '',
    targetDate: i.targetDate ?? '',
  }));
}

describe('ObjectivesEditor — move/remove accessibility', () => {
  it('moving the first objective down lands focus on its (now-enabled) up arrow once it reaches the last position, and announces the move', async () => {
    const user = userEvent.setup();
    render(
      <ObjectivesEditor
        value={objectivesOf({ description: 'first' }, { description: 'second' })}
        testIdPrefix="t"
        onChange={vi.fn()}
        flush={async () => {}}
      />
    );

    await user.click(screen.getByTestId('t-objective-0-down'));

    // The objective that was at index 0 is now last (index 1) — its own
    // "down" arrow is disabled there, so focus lands on "up" instead.
    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('t-objective-1-up')));
    expect(screen.getByTestId('t-objective-1-description')).toHaveValue('first');
    expect(screen.getByText('Objective moved to position 2')).toBeInTheDocument();
  });

  it('moving the last objective up lands focus on its (now-enabled) down arrow once it reaches the first position', async () => {
    const user = userEvent.setup();
    render(
      <ObjectivesEditor
        value={objectivesOf({ description: 'first' }, { description: 'second' })}
        testIdPrefix="t"
        onChange={vi.fn()}
        flush={async () => {}}
      />
    );

    await user.click(screen.getByTestId('t-objective-1-up'));

    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('t-objective-0-down')));
    expect(screen.getByTestId('t-objective-0-description')).toHaveValue('second');
    expect(screen.getByText('Objective moved to position 1')).toBeInTheDocument();
  });

  it('moving a middle objective to another middle position (both arrows stay enabled) keeps focus on the pressed arrow', async () => {
    const user = userEvent.setup();
    render(
      <ObjectivesEditor
        value={objectivesOf(
          { description: 'first' },
          { description: 'second' },
          { description: 'third' },
          { description: 'fourth' }
        )}
        testIdPrefix="t"
        onChange={vi.fn()}
        flush={async () => {}}
      />
    );

    // index 1 -> index 2, still neither the first nor the last of 4 — its own
    // "down" arrow stays enabled at the new position, so focus never needs to move.
    await user.click(screen.getByTestId('t-objective-1-down'));

    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('t-objective-2-down')));
    expect(screen.getByText('Objective moved to position 3')).toBeInTheDocument();
  });

  it('removing an objective focuses the next one\'s description, and announces the removal', async () => {
    const user = userEvent.setup();
    render(
      <ObjectivesEditor
        value={objectivesOf({ description: 'first' }, { description: 'second' })}
        testIdPrefix="t"
        onChange={vi.fn()}
        flush={async () => {}}
      />
    );

    await user.click(screen.getByTestId('t-objective-0-remove'));

    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('t-objective-0-description')));
    expect(screen.getByTestId('t-objective-0-description')).toHaveValue('second');
    expect(screen.getByText('Objective 1 removed, 1 remaining')).toBeInTheDocument();
  });

  it('removing the last objective focuses "Add objective" instead (no next item to land on)', async () => {
    const user = userEvent.setup();
    render(
      <ObjectivesEditor
        value={objectivesOf({ description: 'only' })}
        testIdPrefix="t"
        onChange={vi.fn()}
        flush={async () => {}}
      />
    );

    await user.click(screen.getByTestId('t-objective-0-remove'));

    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('t-objectives-add')));
    expect(screen.getByText('Objective 1 removed, 0 remaining')).toBeInTheDocument();
  });

  it('announces each removal with the position it removed and the count left, even across two removals that land on the same index', async () => {
    const user = userEvent.setup();
    render(
      <ObjectivesEditor
        value={objectivesOf({ description: 'first' }, { description: 'second' }, { description: 'third' }, { description: 'fourth' })}
        testIdPrefix="t"
        onChange={vi.fn()}
        flush={async () => {}}
      />
    );

    // Matches the reported shape: removing the 2nd of 4 leaves 3.
    await user.click(screen.getByTestId('t-objective-1-remove'));
    expect(screen.getByText('Objective 2 removed, 3 remaining')).toBeInTheDocument();

    // The next objective slides into index 0 both times — a fixed string
    // would announce the identical text for both removals.
    await user.click(screen.getByTestId('t-objective-0-remove'));
    expect(screen.getByText('Objective 1 removed, 2 remaining')).toBeInTheDocument();

    await user.click(screen.getByTestId('t-objective-0-remove'));
    expect(screen.getByText('Objective 1 removed, 1 remaining')).toBeInTheDocument();
  });
});
