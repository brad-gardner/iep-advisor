import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { RecentProgressReportsSection } from './recent-progress-reports-section';
import type { ParentProgressReportDto } from '../types';

function renderSection(items: ParentProgressReportDto[]) {
  return render(
    <MemoryRouter>
      <RecentProgressReportsSection items={items} />
    </MemoryRouter>
  );
}

describe('RecentProgressReportsSection', () => {
  it('falls back to "Untitled report" when the server title is null (report created but no file attached yet)', () => {
    renderSection([
      { id: 1, childId: 1, childName: 'Ada Lovelace', title: null, createdAt: '2026-09-01T00:00:00.000Z' },
    ]);
    expect(screen.getByTestId('home-progress-reports-1')).toHaveTextContent('Untitled report');
  });

  it('renders the server title verbatim when present', () => {
    renderSection([
      {
        id: 2,
        childId: 1,
        childName: 'Ada Lovelace',
        title: 'Q3 progress report',
        createdAt: '2026-09-01T00:00:00.000Z',
      },
    ]);
    expect(screen.getByTestId('home-progress-reports-2')).toHaveTextContent('Q3 progress report');
  });
});
