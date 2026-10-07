import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const notificationsApi = vi.hoisted(() => ({ listNotificationFailures: vi.fn() }));
vi.mock('../api/notifications-api', () => notificationsApi);

import { AdminNotificationFailuresPage } from './admin-notification-failures-page';

describe('AdminNotificationFailuresPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and empty state in Spanish', async () => {
    notificationsApi.listNotificationFailures.mockResolvedValue({ success: true, data: [] });
    await renderInSpanish(<AdminNotificationFailuresPage />);

    expect(screen.getByRole('heading', { name: 'Errores de correo de notificaciones' })).toBeInTheDocument();
    expect(await screen.findByText('Sin errores de correo')).toBeInTheDocument();
  });
});
