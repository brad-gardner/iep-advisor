import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { InviteStudentForm } from './invite-student-form';

function renderForm(onInvite = vi.fn().mockResolvedValue({ success: true })) {
  return renderInSpanish(
    <ToastProvider>
      <InviteStudentForm onInvite={onInvite} />
    </ToastProvider>
  );
}

describe('InviteStudentForm in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the card heading, label and submit button in Spanish', async () => {
    await renderForm();

    expect(screen.getByRole('heading', { name: 'Invitar al estudiante' })).toBeInTheDocument();
    expect(screen.getByLabelText('Correo electrónico del estudiante *')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar invitación' })).toBeInTheDocument();
  });
});
