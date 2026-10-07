import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const useAuth = vi.hoisted(() => vi.fn());
vi.mock('@/features/auth/hooks/use-auth', () => ({ useAuth }));

import { OnboardingFlow } from './onboarding-flow';

describe('OnboardingFlow in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the welcome step and progress label in Spanish, and walks through Skip to the state step', async () => {
    useAuth.mockReturnValue({ completeOnboarding: vi.fn() });
    const user = userEvent.setup();
    await renderInSpanish(
      <MemoryRouter>
        <OnboardingFlow />
      </MemoryRouter>
    );

    expect(screen.getByText('Paso 1 de 4')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Le damos la bienvenida a IEP Advisor' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Comencemos su configuración' })).toBeInTheDocument();

    await user.click(screen.getByTestId('onboarding-start'));

    expect(screen.getByRole('heading', { name: 'Indique su estado' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Omitir por ahora' })).toBeInTheDocument();
  });
});
