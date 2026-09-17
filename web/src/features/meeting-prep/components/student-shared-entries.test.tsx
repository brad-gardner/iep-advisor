import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { StudentWorkspaceEntryDto } from '@/features/student/types';

const shareableEntriesApi = vi.hoisted(() => ({ getChildShareableEntries: vi.fn() }));
vi.mock('@/features/student/api/shareable-entries-api', () => shareableEntriesApi);

import { StudentSharedEntries } from './student-shared-entries';

function makeEntry(overrides: Partial<StudentWorkspaceEntryDto> = {}): StudentWorkspaceEntryDto {
  return {
    id: 1,
    entryKind: 'Strength',
    content: 'I am proud of my art.',
    isShareable: true,
    displayOrder: 0,
    createdAt: '2026-09-01T00:00:00.000Z',
    updatedAt: '2026-09-01T00:00:00.000Z',
    ...overrides,
  };
}

describe('StudentSharedEntries', () => {
  beforeEach(() => {
    shareableEntriesApi.getChildShareableEntries.mockReset();
  });

  it('renders stored markdown content as formatted HTML', async () => {
    shareableEntriesApi.getChildShareableEntries.mockResolvedValue({
      success: true,
      data: [makeEntry({ content: 'I am **proud** of my art.' })],
    });
    render(<StudentSharedEntries childId={10} />);

    const content = await screen.findByTestId('student-shared-entry-1-content');
    expect(content.querySelector('strong')).toHaveTextContent('proud');
    expect(content).toHaveTextContent('I am proud of my art.');
  });

  it('renders nothing when there are no shared entries', async () => {
    shareableEntriesApi.getChildShareableEntries.mockResolvedValue({ success: true, data: [] });
    const { container } = render(<StudentSharedEntries childId={10} />);

    await waitFor(() => expect(shareableEntriesApi.getChildShareableEntries).toHaveBeenCalledWith(10));
    expect(container).toBeEmptyDOMElement();
  });
});
