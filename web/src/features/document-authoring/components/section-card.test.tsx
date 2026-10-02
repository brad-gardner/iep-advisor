import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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
}: {
  initialValues: Record<string, unknown>;
  saveValues: (patch: Record<string, unknown>) => Promise<SaveResult>;
  items?: CompletenessItem[];
  disabled?: boolean;
  startOpen?: boolean;
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
});
