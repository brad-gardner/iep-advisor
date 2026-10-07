import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — see
// `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';
import type { OutboundEmailDto } from '../types';

const api = vi.hoisted(() => ({
  listOutboundEmails: vi.fn(),
  getOutboundEmailStatus: vi.fn(),
  resendOutboundEmail: vi.fn(),
  cancelOutboundEmail: vi.fn(),
}));
vi.mock('../api/email-admin-api', () => api);

const showToast = vi.hoisted(() => vi.fn());
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: showToast }) }));

import { AdminEmailPage } from './admin-email-page';

function email(): OutboundEmailDto {
  return {
    id: 1,
    toEmail: 'parent@example.com',
    subject: 'Meeting reminder',
    kind: 'MeetingReminder',
    status: 'Failed',
    attempts: 3,
    lastError: 'ACS send failed: 503',
    nextAttemptAt: '2026-09-16T00:00:00.000Z',
    sentAt: null,
    correlationId: null,
    createdAt: '2026-09-15T00:00:00.000Z',
  };
}

describe('AdminEmailPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, status filter and status badge in Spanish', async () => {
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [email()] });
    api.getOutboundEmailStatus.mockResolvedValue({
      success: true,
      data: { configured: true, queued: 0, failed: 1, sending: 0, lastSentAt: null },
    });

    await renderInSpanish(<AdminEmailPage />, { ns: 'admin' });

    expect(await screen.findByRole('heading', { name: 'Correo saliente' })).toBeInTheDocument();
    expect(screen.getByText('0 en cola · 0 enviándose · 1 fallidos')).toBeInTheDocument();
    expect(await screen.findByTestId('email-status-1')).toHaveTextContent('Fallido');
  });
});
