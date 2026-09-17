import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import type { StudentWorkspaceEntryDto } from '../types';
import { EntryCard } from './entry-card';

function makeEntry(overrides: Partial<StudentWorkspaceEntryDto> = {}): StudentWorkspaceEntryDto {
  return {
    id: 1,
    entryKind: 'Strength',
    content: 'I am good at reading.',
    isShareable: false,
    displayOrder: 0,
    createdAt: '2026-09-01T00:00:00.000Z',
    updatedAt: '2026-09-01T00:00:00.000Z',
    ...overrides,
  };
}

describe('EntryCard', () => {
  it('renders stored markdown content as formatted HTML', () => {
    render(
      <EntryCard
        entry={makeEntry({ content: 'I am **great** at reading.' })}
        onUpdate={vi.fn()}
        onSetShareable={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    const content = screen.getByTestId('entry-1-content');
    expect(content.querySelector('strong')).toHaveTextContent('great');
    expect(content).toHaveTextContent('I am great at reading.');
  });

  it('switches into the RichTextEditor when editing', () => {
    render(
      <EntryCard
        entry={makeEntry()}
        onUpdate={vi.fn()}
        onSetShareable={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    fireEvent.click(screen.getByTestId('entry-1-edit'));
    expect(screen.getByTestId('entry-1-content')).toHaveValue('I am good at reading.');
  });
});
