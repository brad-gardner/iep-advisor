import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ProviderRequestsSection } from './provider-requests-section';
import type { HomeProviderRequestDto } from '../types';

function renderSection(items: HomeProviderRequestDto[]) {
  return render(
    <MemoryRouter>
      <ProviderRequestsSection items={items} />
    </MemoryRouter>
  );
}

describe('ProviderRequestsSection', () => {
  it('renders a "—" fallback (never a blank row) when the due date is null', () => {
    renderSection([{ id: 1, studentId: 10, studentName: 'Ada Lovelace', dueDate: null }]);
    expect(screen.getByTestId('home-provider-requests-1')).toHaveTextContent('Due —');
  });

  it('renders the due date when present', () => {
    renderSection([{ id: 2, studentId: 10, studentName: 'Ada Lovelace', dueDate: '2026-10-01' }]);
    expect(screen.getByTestId('home-provider-requests-2')).toHaveTextContent('Due Oct 1, 2026');
  });
});
