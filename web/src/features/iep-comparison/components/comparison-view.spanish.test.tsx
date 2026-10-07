import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ComparisonResult } from '@/types/api';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const api = vi.hoisted(() => ({ compareIeps: vi.fn() }));
vi.mock('../api/iep-comparison-api', () => api);

import { ComparisonView } from './comparison-view';

const comparison: ComparisonResult = {
  olderIepId: 1,
  newerIepId: 2,
  olderDate: '2026-01-01',
  newerDate: '2026-06-01',
  goalChanges: { added: [], removed: [], modified: [] },
  sectionChanges: { added: [], removed: [], inBoth: [] },
  redFlagResolution: { resolved: [], persisting: [], newFlags: [] },
  summary: {
    goalsAdded: 0,
    goalsRemoved: 0,
    goalsModified: 0,
    goalsUnchanged: 0,
    sectionsAdded: 0,
    sectionsRemoved: 0,
    redFlagsResolved: 0,
    redFlagsPersisting: 0,
    newRedFlags: 0,
  },
};

describe('ComparisonView in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the title, breadcrumb and goal-changes heading in Spanish', async () => {
    api.compareIeps.mockResolvedValue({ success: true, data: comparison });

    await renderInSpanish(
      <MemoryRouter>
        <ComparisonView iepId={1} otherId={2} childId={4} />
      </MemoryRouter>,
      { ns: 'iep-comparison' },
    );

    expect(await screen.findByRole('heading', { name: 'Comparación de IEP' })).toBeInTheDocument();
    expect(screen.getByText('Volver al hijo')).toBeInTheDocument();
    expect(screen.getByText('Cambios en las metas')).toBeInTheDocument();
    expect(screen.getByText('No se detectaron cambios en las metas entre estos IEP.')).toBeInTheDocument();
  });
});
