import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const subscriptionApi = vi.hoisted(() => ({ getSubscriptionStatus: vi.fn() }));
vi.mock('../api/subscription-api', () => subscriptionApi);

import { SubscriptionPage } from './subscription-page';

describe('SubscriptionPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, status badge and invite-code link in Spanish', async () => {
    subscriptionApi.getSubscriptionStatus.mockResolvedValue({
      status: 'active',
      expiresAt: '2099-01-01T00:00:00Z',
      childUsage: null,
    });

    await renderInSpanish(
      <ToastProvider>
        <MemoryRouter>
          <SubscriptionPage />
        </MemoryRouter>
      </ToastProvider>
    );

    expect(screen.getByRole('heading', { name: 'Suscripción', level: 1 })).toBeInTheDocument();
    expect(await screen.findByTestId('subscription-status')).toHaveTextContent('Activa');
    expect(screen.getByText('Su plan')).toBeInTheDocument();
    expect(screen.getByText('¿Tiene un código de invitación?')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Canjéelo aquí' })).toBeInTheDocument();
  });

  it('shows the Spanish retry button when the status fails to load', async () => {
    subscriptionApi.getSubscriptionStatus.mockRejectedValue(new Error('network'));

    await renderInSpanish(
      <MemoryRouter>
        <SubscriptionPage />
      </MemoryRouter>
    );

    expect(await screen.findByText('No se pudo cargar el estado de la suscripción')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Intentar de nuevo' })).toBeInTheDocument();
  });
});
