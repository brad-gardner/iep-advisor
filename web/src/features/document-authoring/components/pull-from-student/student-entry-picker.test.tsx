import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StudentEntryPicker } from './student-entry-picker';
import type { StudentWorkspaceEntryDto } from '@/features/student/types';

function makeEntry(overrides: Partial<StudentWorkspaceEntryDto> = {}): StudentWorkspaceEntryDto {
  return {
    id: 1,
    entryKind: 'Strength',
    content: 'I am good at [reading](https://example.com/reading) out loud.',
    isShareable: true,
    displayOrder: 0,
    createdAt: '2026-01-01T00:00:00.000Z',
    updatedAt: '2026-01-01T00:00:00.000Z',
    ...overrides,
  };
}

describe('StudentEntryPicker', () => {
  it('renders an entry containing a markdown link without nesting an <a> inside the option button', () => {
    render(
      <StudentEntryPicker
        entries={[makeEntry()]}
        isLoading={false}
        error={false}
        onPick={vi.fn()}
        testIdPrefix="pull-from-student"
      />
    );

    const option = screen.getByTestId('pull-from-student-option-1');
    expect(option.tagName).toBe('BUTTON');
    // A <button role="option"> containing an <a href> is invalid HTML
    // (interactive content nested in interactive content) — disableLinks on
    // the entry's Markdown must render the link text as plain text instead.
    expect(option.querySelector('a')).toBeNull();
    expect(option).toHaveTextContent('reading');
  });
});
