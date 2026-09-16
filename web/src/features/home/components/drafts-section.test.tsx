import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { DraftsSection } from './drafts-section';
import { makeHomeDraft } from '../test/fixtures';

function renderSection(...args: Parameters<typeof makeHomeDraft>) {
  return render(
    <MemoryRouter>
      <DraftsSection drafts={[makeHomeDraft(...args)]} />
    </MemoryRouter>
  );
}

describe('DraftsSection', () => {
  it('renders a "—" fallback (never a blank row) for a draft never edited', () => {
    renderSection({ instanceId: 1, lastEditedAt: null });
    expect(screen.getByTestId('home-drafts-1')).toHaveTextContent('edited —');
  });

  it('renders the edited date when present', () => {
    renderSection({ instanceId: 2, lastEditedAt: '2026-09-10T12:00:00.000Z' });
    expect(screen.getByTestId('home-drafts-2')).toHaveTextContent('edited Sep 10, 2026');
  });
});
