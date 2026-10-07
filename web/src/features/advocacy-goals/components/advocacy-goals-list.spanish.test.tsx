import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { AdvocacyGoalsList } from './advocacy-goals-list';

describe('AdvocacyGoalsList in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the empty state in Spanish', async () => {
    await renderInSpanish(
      <ToastProvider>
        <AdvocacyGoalsList childId={4} childName="Jordan" goals={[]} isLoading={false} onReload={() => {}} />
      </ToastProvider>
    );

    expect(screen.getByText('Defina sus prioridades para Jordan')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Agregue su primera meta' })).toBeInTheDocument();
  });

  it('renders the goal list controls in Spanish', async () => {
    await renderInSpanish(
      <ToastProvider>
        <AdvocacyGoalsList
          childId={4}
          childName="Jordan"
          goals={[
            {
              id: 1,
              childProfileId: 4,
              goalText: 'Improve reading fluency',
              category: 'academic',
              displayOrder: 1,
              createdAt: '2026-01-01T00:00:00.000Z',
              updatedAt: '2026-01-01T00:00:00.000Z',
            },
          ]}
          isLoading={false}
          onReload={() => {}}
        />
      </ToastProvider>
    );

    expect(screen.getByText('1/10 metas')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '+ Agregar meta' })).toBeInTheDocument();
    expect(screen.getByText('Académica')).toBeInTheDocument();
  });
});
