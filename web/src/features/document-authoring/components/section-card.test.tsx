import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { TemplateSectionDto } from '../types';
import type { CompletenessItem } from '../lib/completeness';
import type { SaveResult } from '../hooks/use-document-instance';
import { SectionCard } from './section-card';

const FIELD_A = 'field-a';
const FIELD_B = 'field-b';

function section(): TemplateSectionDto {
  return {
    id: 10,
    sectionKey: 'profile',
    title: 'Student Profile',
    displayOrder: 0,
    fields: [
      { id: 100, fieldKey: FIELD_A, fieldType: 'Text', label: 'Name', required: true, displayOrder: 0, configJson: null },
      { id: 101, fieldKey: FIELD_B, fieldType: 'Text', label: 'Preferred name', required: false, displayOrder: 1, configJson: null },
    ],
  };
}

const GOALS_FIELD = 'goals-field';
const goalTextCol = 'c1111111-1111-1111-1111-111111111111';

function goalsSection(): TemplateSectionDto {
  return {
    id: 20,
    sectionKey: 'goals',
    title: 'Goals',
    displayOrder: 0,
    fields: [
      {
        id: 200,
        fieldKey: GOALS_FIELD,
        fieldType: 'Table',
        label: 'Goals',
        required: false,
        displayOrder: 0,
        configJson: JSON.stringify({
          semantic: 'goals',
          columns: [{ columnKey: goalTextCol, type: 'Text', label: 'Goal', required: true, semantic: 'goalText' }],
        }),
      },
    ],
  };
}

function GoalsHarness({
  initialValues,
  saveValues,
  startOpen = false,
}: {
  initialValues: Record<string, unknown>;
  saveValues: (patch: Record<string, unknown>) => Promise<SaveResult>;
  startOpen?: boolean;
}) {
  const [isOpen, setIsOpen] = useState(startOpen);
  return (
    <SectionCard
      section={goalsSection()}
      values={initialValues}
      disabled={false}
      saveValues={saveValues}
      isOpen={isOpen}
      onOpen={() => setIsOpen(true)}
      onClose={() => setIsOpen(false)}
      items={[]}
    />
  );
}

function okResult(values: Record<string, unknown>): SaveResult {
  return { ok: true, values };
}

/** Mirrors how DocumentEditor owns `isOpen` via useSectionEditing — SectionCard
 *  itself is a controlled component. */
function Harness({
  initialValues,
  saveValues,
  items = [],
  disabled = false,
  startOpen = false,
  registerFailureStatus,
}: {
  initialValues: Record<string, unknown>;
  saveValues: (patch: Record<string, unknown>) => Promise<SaveResult>;
  items?: CompletenessItem[];
  disabled?: boolean;
  startOpen?: boolean;
  registerFailureStatus?: (hasFailures: () => boolean) => () => void;
}) {
  const [isOpen, setIsOpen] = useState(startOpen);
  return (
    <SectionCard
      section={section()}
      values={initialValues}
      disabled={disabled}
      saveValues={saveValues}
      isOpen={isOpen}
      onOpen={() => setIsOpen(true)}
      onClose={() => setIsOpen(false)}
      items={items}
      registerFailureStatus={registerFailureStatus}
    />
  );
}

describe('SectionCard — read mode', () => {
  it('renders each field read-only with an Edit button', () => {
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan Ellis', [FIELD_B]: 'Jordan' }} saveValues={vi.fn()} />);
    expect(screen.getByTestId(`read-field-${FIELD_A}`)).toHaveTextContent('Jordan Ellis');
    expect(screen.getByTestId(`read-field-${FIELD_B}`)).toHaveTextContent('Jordan');
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument();
    expect(screen.queryByTestId('section-10-done')).not.toBeInTheDocument();
  });

  it('shows each field\'s own label when the section has more than one field, and hides it when the section has exactly one (the section heading already names it)', () => {
    const { unmount } = render(
      <Harness initialValues={{ [FIELD_A]: 'Jordan Ellis', [FIELD_B]: 'Jordan' }} saveValues={vi.fn()} />
    );
    expect(screen.getByText('Name')).toBeInTheDocument();
    expect(screen.getByText('Preferred name')).toBeInTheDocument();
    unmount();

    const oneFieldSection: TemplateSectionDto = { ...section(), fields: [section().fields[0]] };
    render(
      <SectionCard
        section={oneFieldSection}
        values={{ [FIELD_A]: 'Jordan Ellis' }}
        disabled={false}
        saveValues={vi.fn()}
        isOpen={false}
        onOpen={() => {}}
        onClose={() => {}}
        items={[]}
      />
    );
    expect(screen.queryByText('Name')).not.toBeInTheDocument();
    expect(screen.getByTestId(`read-field-${FIELD_A}`)).toHaveTextContent('Jordan Ellis');
  });

  it('shows "Not started" with a Start editing link when every field is blank', async () => {
    const user = userEvent.setup();
    render(<Harness initialValues={{ [FIELD_A]: '', [FIELD_B]: '' }} saveValues={vi.fn()} />);
    expect(screen.getByTestId('section-10-start-editing')).toHaveTextContent('Start editing');

    await user.click(screen.getByTestId('section-10-start-editing'));
    expect(await screen.findByTestId('section-10-done')).toBeInTheDocument();
  });

  it('badge: shows the required-item count when closed with a required gap', () => {
    render(
      <Harness
        initialValues={{ [FIELD_A]: '', [FIELD_B]: '' }}
        saveValues={vi.fn()}
        items={[{ key: 'r1', severity: 'required', message: 'Name is required', fieldKey: FIELD_A, fieldId: 100, sectionId: 10 }]}
      />
    );
    expect(screen.getByTestId('section-10-status')).toHaveTextContent('1 required item');
  });

  it('badge: shows an advisory "to review" count when there is no required gap', () => {
    render(
      <Harness
        initialValues={{ [FIELD_A]: 'x', [FIELD_B]: '' }}
        saveValues={vi.fn()}
        items={[{ key: 'a1', severity: 'advisory', message: 'Review this', fieldKey: FIELD_A, fieldId: 100, sectionId: 10 }]}
      />
    );
    expect(screen.getByTestId('section-10-status')).toHaveTextContent('1 item to review');
  });

  it('disables Edit (and Start editing) while the document is read-only', () => {
    render(<Harness initialValues={{ [FIELD_A]: '', [FIELD_B]: '' }} saveValues={vi.fn()} disabled />);
    expect(screen.getByTestId('section-10-start-editing')).toBeDisabled();
  });
});

describe('SectionCard — Edit / Done', () => {
  it('Edit opens the section, shows the Editing badge, and focuses the first field', async () => {
    const user = userEvent.setup();
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={vi.fn()} />);

    await user.click(screen.getByTestId('section-10-edit'));
    expect(screen.getByTestId('section-10-status')).toHaveTextContent('Editing');
    const firstInput = screen.getByTestId(`field-${FIELD_A}`);
    await waitFor(() => expect(document.activeElement).toBe(firstInput));
  });

  it('Done flushes the pending edit (before its own debounce fires) and closes, returning focus to Edit', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => Promise.resolve(okResult(patch)));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} />);

    await user.click(screen.getByTestId('section-10-edit'));
    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Jordan Ellis');

    await user.click(screen.getByTestId('section-10-done'));

    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Jordan Ellis' }));
    expect(screen.queryByTestId('section-10-done')).not.toBeInTheDocument();
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument();
    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('section-10-edit')));
  });
});

describe('SectionCard — Discard changes', () => {
  it('discards immediately (no confirmation, no save) when nothing has autosaved yet', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockResolvedValue(okResult({}));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.click(screen.getByTestId('section-10-discard'));
    expect(saveValues).not.toHaveBeenCalled();
    expect(screen.queryByTestId('section-10-discard-confirm')).not.toBeInTheDocument();
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument();
  });

  it('confirms, then restores the pre-Edit snapshot for every field in the section via one save call', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => Promise.resolve(okResult(patch)));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: 'JJ' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab(); // blur flushes the pending autosave
    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Changed' }));

    saveValues.mockClear();
    await user.click(screen.getByTestId('section-10-discard'));
    expect(await screen.findByTestId('section-10-discard-confirm')).toBeInTheDocument();

    await user.click(screen.getByTestId('section-10-discard-confirm'));
    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Jordan', [FIELD_B]: 'JJ' }));
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument();
  });

  it('"Keep editing" cancels the confirmation without saving', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => Promise.resolve(okResult(patch)));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab();
    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Changed' }));
    saveValues.mockClear();

    await user.click(screen.getByTestId('section-10-discard'));
    await user.click(screen.getByTestId('section-10-discard-cancel'));

    expect(saveValues).not.toHaveBeenCalled();
    expect(screen.getByTestId('section-10-done')).toBeInTheDocument(); // still open
  });

  it('a failed restore (409) shows an error and keeps the section open', async () => {
    const user = userEvent.setup();
    const saveValues = vi
      .fn()
      .mockResolvedValueOnce(okResult({ [FIELD_A]: 'Changed' }))
      .mockResolvedValueOnce({ ok: false, conflict: true });
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab();
    await waitFor(() => expect(saveValues).toHaveBeenCalledTimes(1));

    await user.click(screen.getByTestId('section-10-discard'));
    await user.click(screen.getByTestId('section-10-discard-confirm'));

    expect(await screen.findByRole('alert')).toHaveTextContent('changed elsewhere');
    expect(screen.getByTestId('section-10-discard-confirm')).toBeInTheDocument(); // still in the confirm state, still open
  });

  it('typing then Discarding immediately — before the debounce has even fired — still flushes and restores (nothing is silently kept)', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => Promise.resolve(okResult(patch)));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    // Discard immediately — the 700ms autosave debounce has not fired on its own.
    await user.click(screen.getByTestId('section-10-discard'));

    expect(await screen.findByTestId('section-10-discard-confirm')).toBeInTheDocument();
    await user.click(screen.getByTestId('section-10-discard-confirm'));

    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Jordan', [FIELD_B]: '' }));
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument();
  });

  it('a save already in flight when Discard is clicked is still accounted for (not raced by the unmount flush)', async () => {
    const user = userEvent.setup();
    let resolveFirst!: (r: SaveResult) => void;
    const saveValues = vi
      .fn()
      .mockImplementationOnce(() => new Promise<SaveResult>((resolve) => (resolveFirst = resolve)))
      .mockImplementation((patch: Record<string, unknown>) => Promise.resolve(okResult(patch)));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab(); // blur flushes immediately — this save is now in flight, unresolved
    await waitFor(() => expect(saveValues).toHaveBeenCalledTimes(1));

    const discardClick = user.click(screen.getByTestId('section-10-discard'));
    resolveFirst(okResult({ [FIELD_A]: 'Changed' }));
    await discardClick;

    expect(await screen.findByTestId('section-10-discard-confirm')).toBeInTheDocument();
  });

  it('confirm path: the restore is the last save — the field unmounting afterward never resurrects the discarded edit', async () => {
    const user = userEvent.setup();
    const calls: Array<Record<string, unknown>> = [];
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      calls.push(patch);
      return Promise.resolve(okResult(patch));
    });
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: 'JJ' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.click(screen.getByTestId('section-10-discard'));
    await user.click(await screen.findByTestId('section-10-discard-confirm'));

    await waitFor(() => expect(screen.getByTestId('section-10-edit')).toBeInTheDocument());
    expect(calls.at(-1)).toEqual({ [FIELD_A]: 'Jordan', [FIELD_B]: 'JJ' }); // the restore, landing last
    expect(calls.filter((c) => c[FIELD_A] === 'Changed')).toHaveLength(1); // the one flushed edit, never repeated after the restore
  });
});

describe('SectionCard — Discard confirmation disables editing', () => {
  it('disables the fields the instant confirmation appears, and disables both confirmation buttons while the restore is in flight', async () => {
    const user = userEvent.setup();
    let resolveRestore!: (r: SaveResult) => void;
    const saveValues = vi
      .fn()
      .mockImplementationOnce((patch: Record<string, unknown>) => Promise.resolve(okResult(patch))) // the one flushed edit
      .mockImplementationOnce(() => new Promise<SaveResult>((resolve) => (resolveRestore = resolve))); // the restore
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab();
    await waitFor(() => expect(saveValues).toHaveBeenCalledTimes(1));

    await user.click(screen.getByTestId('section-10-discard'));
    await screen.findByTestId('section-10-discard-confirm');
    // Nothing can race the confirmation — both fields are disabled the moment
    // it appears, before the user ever gets to decide.
    expect(screen.getByTestId(`field-${FIELD_A}`)).toBeDisabled();
    expect(screen.getByTestId(`field-${FIELD_B}`)).toBeDisabled();

    const confirmClick = user.click(screen.getByTestId('section-10-discard-confirm'));
    await waitFor(() => expect(saveValues).toHaveBeenCalledTimes(2));
    // The restore is now in flight — both buttons are disabled so neither
    // "Keep editing" nor a second "Discard changes" click can race it.
    await waitFor(() => expect(screen.getByTestId('section-10-discard-confirm')).toBeDisabled());
    expect(screen.getByTestId('section-10-discard-cancel')).toBeDisabled();
    // Still disabled throughout — the restore hasn't resolved yet.
    expect(screen.getByTestId(`field-${FIELD_A}`)).toBeDisabled();

    resolveRestore(okResult({ [FIELD_A]: 'Jordan', [FIELD_B]: '' }));
    await confirmClick;
    await waitFor(() => expect(screen.getByTestId('section-10-edit')).toBeInTheDocument());
  });
});

describe('SectionCard — Discard confirmation accessibility', () => {
  it('focuses "Keep editing" when the confirmation appears, returns focus to Discard on cancel, and announces as an alert', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => Promise.resolve(okResult(patch)));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');

    await user.click(screen.getByTestId('section-10-discard'));
    const keepEditing = await screen.findByTestId('section-10-discard-cancel');
    await waitFor(() => expect(document.activeElement).toBe(keepEditing));
    expect(screen.getByRole('alert')).toHaveTextContent('Discard the changes saved since you started editing?');

    await user.click(keepEditing);
    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('section-10-discard')));
  });
});

describe('SectionCard — Done with a failed save', () => {
  it('does not close when the flushed save fails, and the failure stays visible', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockResolvedValue({ ok: false, message: 'Something went wrong.' });
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.click(screen.getByTestId('section-10-done'));

    await waitFor(() => expect(saveValues).toHaveBeenCalled());
    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't save Name");
    expect(screen.getByTestId('section-10-retry')).toBeInTheDocument();
    expect(screen.getByTestId('section-10-done')).toBeInTheDocument(); // still open
    expect(screen.queryByTestId('section-10-edit')).not.toBeInTheDocument();
  });

  it('field A fails then field B succeeds — Done stays open with the alert still naming A (a later, unrelated success never masks it)', async () => {
    const user = userEvent.setup();
    const saveValues = vi
      .fn()
      .mockImplementation((patch: Record<string, unknown>) =>
        Promise.resolve(FIELD_A in patch ? { ok: false, message: 'nope' } : okResult(patch))
      );
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab(); // blur flushes A immediately — fails
    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Changed' }));
    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't save Name");

    await user.type(screen.getByTestId(`field-${FIELD_B}`), 'JJ');
    await user.tab(); // blur flushes B immediately — succeeds
    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_B]: 'JJ' }));

    // B's success must not clear A's still-outstanding failure.
    expect(screen.getByRole('alert')).toHaveTextContent("Couldn't save Name");

    await user.click(screen.getByTestId('section-10-done'));
    expect(screen.getByTestId('section-10-done')).toBeInTheDocument(); // still open
  });

  it('Retry resends the exact value that failed, and Done can then close once it succeeds', async () => {
    const user = userEvent.setup();
    let failNextA = true;
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      if (FIELD_A in patch && failNextA) {
        failNextA = false;
        return Promise.resolve({ ok: false, message: 'nope' });
      }
      return Promise.resolve(okResult(patch));
    });
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab();
    expect(await screen.findByTestId('section-10-retry')).toBeInTheDocument();

    saveValues.mockClear();
    await user.click(screen.getByTestId('section-10-retry'));
    // The retry resends FIELD_A's own last-attempted value — not the stale,
    // pre-edit value from `values` (that would silently save over the edit
    // instead of recovering it).
    await waitFor(() => expect(saveValues).toHaveBeenCalledWith({ [FIELD_A]: 'Changed' }));
    await waitFor(() => expect(screen.queryByTestId('section-10-retry')).not.toBeInTheDocument());

    await user.click(screen.getByTestId('section-10-done'));
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument(); // closed
  });

  it('closes once the user fixes the edit and a later Done succeeds, after an earlier attempt failed', async () => {
    const user = userEvent.setup();
    const saveValues = vi
      .fn()
      .mockResolvedValueOnce({ ok: false, message: 'nope' })
      .mockResolvedValueOnce(okResult({ [FIELD_A]: 'Changed again' }));
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.click(screen.getByTestId('section-10-done'));
    await waitFor(() => expect(saveValues).toHaveBeenCalledTimes(1));
    expect(screen.getByTestId('section-10-done')).toBeInTheDocument(); // still open after the failure

    // {ok: false} is a normal (non-throwing) result, so the field's own
    // autosave has nothing left queued to retry on its own — Done only
    // succeeds once there is a new edit to flush.
    await user.type(screen.getByTestId(`field-${FIELD_A}`), ' again');
    await user.click(screen.getByTestId('section-10-done'));
    await waitFor(() => expect(saveValues).toHaveBeenCalledTimes(2));
    expect(screen.getByTestId('section-10-edit')).toBeInTheDocument();
  });
});

describe('SectionCard — Discard after a failed save clears the failure record', () => {
  it('a successful restore clears failedFieldsRef before closing: no banner/Retry once closed, and the registered failure getter reports false', async () => {
    const user = userEvent.setup();
    // Fails only the EDITED value — the restore re-sends the original
    // ('Jordan'), which must succeed.
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) =>
      Promise.resolve(patch[FIELD_A] === 'Changed' ? { ok: false, message: 'nope' } : okResult(patch))
    );
    let hasFailures: (() => boolean) | undefined;
    render(
      <Harness
        initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }}
        saveValues={saveValues}
        startOpen
        registerFailureStatus={(getter) => {
          hasFailures = getter;
          return () => {};
        }}
      />
    );

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'Changed');
    await user.tab(); // blur flushes immediately — fails
    expect(await screen.findByTestId('section-10-retry')).toBeInTheDocument();
    expect(hasFailures?.()).toBe(true);

    await user.click(screen.getByTestId('section-10-discard'));
    await user.click(await screen.findByTestId('section-10-discard-confirm'));

    // Closed, with nothing left behind.
    await waitFor(() => expect(screen.getByTestId('section-10-edit')).toBeInTheDocument());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByTestId('section-10-retry')).not.toBeInTheDocument();
    // The registered getter — what Finalize actually consults — also agrees
    // the section is clean, so Finalize is free to proceed.
    expect(hasFailures?.()).toBe(false);
  });
});

describe('SectionCard — Retry resends the latest value', () => {
  it('flushes a newer, still-pending edit before rebuilding the retry patch, so the stale failed value is never resent behind it', async () => {
    const user = userEvent.setup();
    const aValues: string[] = [];
    let failNextA = true;
    const saveValues = vi.fn().mockImplementation((patch: Record<string, unknown>) => {
      if (FIELD_A in patch) {
        aValues.push(patch[FIELD_A] as string);
        if (failNextA) {
          failNextA = false;
          return Promise.resolve({ ok: false, message: 'nope' });
        }
      }
      return Promise.resolve(okResult(patch));
    });
    render(<Harness initialValues={{ [FIELD_A]: 'Jordan', [FIELD_B]: '' }} saveValues={saveValues} startOpen />);

    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'V1');
    await user.tab(); // blur flushes V1 immediately — fails
    expect(await screen.findByTestId('section-10-retry')).toBeInTheDocument();

    // A newer edit to the SAME field — still sitting in its own debounce,
    // not yet flushed — when Retry is clicked.
    await user.clear(screen.getByTestId(`field-${FIELD_A}`));
    await user.type(screen.getByTestId(`field-${FIELD_A}`), 'V2');

    // Blur (the field's own flush, same as a real mousedown-before-click on
    // the Retry button) and the Retry click fired back to back with no
    // await between them, so V2's save is still in flight — not yet
    // settled — the instant Retry's own handler starts building its patch.
    fireEvent.blur(screen.getByTestId(`field-${FIELD_A}`));
    fireEvent.click(screen.getByTestId('section-10-retry'));

    await waitFor(() => expect(screen.queryByTestId('section-10-retry')).not.toBeInTheDocument());
    // V1 was sent exactly once (the original failed attempt); V2 is the
    // last value sent for this field — never resent behind a stale V1.
    expect(aValues).toEqual(['V1', 'V2']);
  });
});

describe('SectionCard — focus on Edit for a card-list field (Goals/Services)', () => {
  it('falls back to the first focusable BUTTON when the opened view has no input (card list, nothing expanded)', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockResolvedValue(okResult({}));
    render(<GoalsHarness initialValues={{ [GOALS_FIELD]: [] }} saveValues={saveValues} />);

    await user.click(screen.getByTestId('section-20-edit'));
    // No input/textarea/select exists in the empty card-list view — "Add goal"
    // is the nearest focusable control.
    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId(`field-${GOALS_FIELD}-add`)));
  });

  it('skips its own focus entirely when a specific row requested it, instead of fighting that row editor for focus', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockResolvedValue(okResult({}));
    render(
      <GoalsHarness initialValues={{ [GOALS_FIELD]: [{ _rowId: 'G-1', [goalTextCol]: 'Read better' }] }} saveValues={saveValues} />
    );

    // Closed (read mode): the goal's own "Edit goal" requests row-level focus.
    await user.click(screen.getByTestId('read-goal-0-edit'));

    // The row's own focused editor claims focus on its own mount — never
    // overridden a tick later by the section's generic "first input" query.
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('textbox', { name: /^Goal\s*\*?$/ })));
  });
});
