import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const notificationsApi = vi.hoisted(() => ({
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
}));
vi.mock('../api/notifications-api', () => notificationsApi);

import { NotificationsProvider } from '../stores/notifications-context';
import { NotificationBell } from './notification-bell';

function renderBell() {
  return renderInSpanish(
    <MemoryRouter>
      <NotificationsProvider>
        <NotificationBell />
      </NotificationsProvider>
    </MemoryRouter>
  );
}

describe('NotificationBell in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('translates the unread aria-label, the live region, and "See all"', async () => {
    notificationsApi.listNotifications.mockResolvedValue({
      success: true,
      data: { items: [], unreadCount: 2 },
    });
    const user = userEvent.setup();
    await renderBell();

    expect(await screen.findByRole('button', { name: 'Notificaciones, 2 sin leer' })).toBeInTheDocument();
    expect(screen.getByTestId('notification-unread-live')).toHaveTextContent('2 notificaciones sin leer');

    await user.click(screen.getByTestId('notification-bell'));
    expect(await screen.findByText('Aún no hay notificaciones.')).toBeInTheDocument();
    expect(screen.getByText('Ver todas')).toBeInTheDocument();
  });
});
