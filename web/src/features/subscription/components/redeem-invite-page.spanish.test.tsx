import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

vi.mock('../api/subscription-api', () => ({ redeemInvite: vi.fn() }));

import { RedeemInvitePage } from './redeem-invite-page';

describe('RedeemInvitePage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, description and submit button in Spanish', async () => {
    await renderInSpanish(
      <ToastProvider>
        <RedeemInvitePage />
      </ToastProvider>
    );

    expect(screen.getByRole('heading', { name: 'Canjear código de invitación' })).toBeInTheDocument();
    expect(
      screen.getByText('Ingrese el código de invitación de 8 caracteres que recibió para activar su suscripción.')
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Código de invitación')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Canjear' })).toBeInTheDocument();
  });
});
