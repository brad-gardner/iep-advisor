import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

vi.mock('../api/sharing-api', () => ({ createInvite: vi.fn() }));

import { ShareChildDialog } from './share-child-dialog';

describe('ShareChildDialog in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, field labels, role options, and action buttons in Spanish', async () => {
    await renderInSpanish(
      <ToastProvider>
        <ShareChildDialog childId={1} onInvited={vi.fn()} onCancel={vi.fn()} />
      </ToastProvider>
    );

    expect(screen.getByText('Compartir acceso')).toBeInTheDocument();
    expect(screen.getByLabelText('Correo electrónico')).toBeInTheDocument();
    expect(screen.getByLabelText('Rol')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Observador' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Colaborador' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Enviar invitación/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeInTheDocument();
  });
});
