import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import type { NotificationDto } from '../types';

const notificationsApi = vi.hoisted(() => ({
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}));
vi.mock('../api/notifications-api', () => notificationsApi);

import { NotificationsProvider } from '../stores/notifications-context';
import { NotificationsPage } from './notifications-page';

function renderPage() {
  return render(
    <ToastProvider>
      <MemoryRouter>
        <NotificationsProvider>
          <NotificationsPage />
        </NotificationsProvider>
      </MemoryRouter>
    </ToastProvider>
  );
}

const items: NotificationDto[] = [
  {
    id: 1,
    kind: 'MeetingScheduled',
    title: 'Meeting scheduled',
    body: 'Annual review on Oct 1',
    linkPath: '/educator/calendar',
    createdAt: '2026-09-14T12:00:00Z',
    readAt: null,
    emailSentAt: '2026-09-14T12:01:00Z',
    emailError: null,
  },
  {
    id: 2,
    kind: 'ObligationDigest',
    title: 'Weekly digest',
    body: '1 obligation due soon',
    linkPath: null,
    createdAt: '2026-09-10T07:00:00Z',
    readAt: '2026-09-10T08:00:00Z',
    emailSentAt: null,
    emailError: 'SMTP timeout',
  },
];

describe('NotificationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    notificationsApi.listNotifications.mockResolvedValue({
      success: true,
      data: { items, unreadCount: 1 },
    });
  });

  it('lists notifications with unread indicated and an email failure surfaced', async () => {
    renderPage();
    expect(await screen.findByTestId('notification-1')).toHaveTextContent('Meeting scheduled');
    expect(screen.getByTestId('notification-1')).toHaveTextContent('New');
    expect(screen.getByTestId('notification-2')).toHaveTextContent('SMTP timeout');
  });

  it('marks a single notification read on click', async () => {
    const user = userEvent.setup();
    notificationsApi.markNotificationRead.mockResolvedValue({ success: true, data: null });
    renderPage();
    await screen.findByTestId('notification-1');

    await user.click(screen.getByTestId('notification-1'));
    await waitFor(() => expect(notificationsApi.markNotificationRead).toHaveBeenCalledWith(1));
    await waitFor(() => expect(screen.getByTestId('notification-1')).not.toHaveTextContent('New'));
  });

  it('refreshes the shared unread count after marking a notification read', async () => {
    const user = userEvent.setup();
    notificationsApi.markNotificationRead.mockResolvedValue({ success: true, data: null });
    renderPage();
    await screen.findByTestId('notification-1');
    // The provider's own initial poll, plus the page's own list load.
    const callsBeforeClick = notificationsApi.listNotifications.mock.calls.length;

    await user.click(screen.getByTestId('notification-1'));
    await waitFor(() => expect(notificationsApi.markNotificationRead).toHaveBeenCalledWith(1));
    // `refresh()` fires an extra unread-count fetch beyond the initial poll/load.
    await waitFor(() =>
      expect(notificationsApi.listNotifications.mock.calls.length).toBeGreaterThan(callsBeforeClick)
    );
  });

  it('marks all as read', async () => {
    const user = userEvent.setup();
    notificationsApi.markAllNotificationsRead.mockResolvedValue({ success: true, data: { marked: 1 } });
    renderPage();
    await screen.findByTestId('notification-1');

    await user.click(screen.getByTestId('notifications-mark-all-read'));
    await waitFor(() => expect(notificationsApi.markAllNotificationsRead).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByTestId('notifications-mark-all-read')).toBeDisabled());
  });

  it('shows an empty state when there are no notifications', async () => {
    notificationsApi.listNotifications.mockResolvedValue({ success: true, data: { items: [], unreadCount: 0 } });
    renderPage();
    expect(await screen.findByText('No notifications yet')).toBeInTheDocument();
  });

  it('surfaces a load failure as an inline alert with a retry affordance', async () => {
    notificationsApi.listNotifications.mockRejectedValueOnce(apiRejection('Could not load notifications'));
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load notifications');

    const user = userEvent.setup();
    notificationsApi.listNotifications.mockResolvedValue({ success: true, data: { items, unreadCount: 1 } });
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    await screen.findByTestId('notification-1');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
