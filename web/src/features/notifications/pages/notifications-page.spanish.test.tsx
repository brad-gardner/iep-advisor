import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const notificationsApi = vi.hoisted(() => ({
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}));
vi.mock('../api/notifications-api', () => notificationsApi);

import { NotificationsProvider } from '../stores/notifications-context';
import { NotificationsPage } from './notifications-page';

function renderPage() {
  return renderInSpanish(
    <ToastProvider>
      <MemoryRouter>
        <NotificationsProvider>
          <NotificationsPage />
        </NotificationsProvider>
      </MemoryRouter>
    </ToastProvider>
  );
}

describe('NotificationsPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the empty state and "Mark all read" button in Spanish', async () => {
    notificationsApi.listNotifications.mockResolvedValue({
      success: true,
      data: { items: [], unreadCount: 0 },
    });
    await renderPage();

    expect(screen.getByRole('heading', { name: 'Notificaciones' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Marcar todas como leídas' })).toBeInTheDocument();
    expect(await screen.findByText('Aún no hay notificaciones')).toBeInTheDocument();
    expect(screen.getByText('Las actualizaciones y recordatorios de reuniones aparecerán aquí.')).toBeInTheDocument();
  });
});
