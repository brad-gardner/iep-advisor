import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const notificationsApi = vi.hoisted(() => ({
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
}));
vi.mock('../api/notifications-api', () => notificationsApi);

import { NotificationsProvider } from '../stores/notifications-context';
import { NotificationBell } from './notification-bell';

function renderBell() {
  return render(
    <MemoryRouter>
      <NotificationsProvider>
        <NotificationBell />
      </NotificationsProvider>
    </MemoryRouter>
  );
}

const notification = {
  id: 1,
  kind: 'MeetingScheduled' as const,
  title: 'Meeting scheduled',
  body: 'Annual review on Oct 1',
  linkPath: '/educator/calendar',
  createdAt: '2026-09-15T12:00:00Z',
  readAt: null,
  emailSentAt: null,
  emailError: null,
};

describe('NotificationBell', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    notificationsApi.listNotifications.mockResolvedValue({
      success: true,
      data: { items: [notification], unreadCount: 3 },
    });
    notificationsApi.markNotificationRead.mockResolvedValue({ success: true, data: null });
  });

  it('shows the unread count as a live-region announcement and a badge', async () => {
    renderBell();
    await waitFor(() => expect(screen.getByTestId('notification-bell-badge')).toHaveTextContent('3'));
    expect(screen.getByTestId('notification-unread-live')).toHaveTextContent('3 unread notifications');
    expect(screen.getByTestId('notification-unread-live')).toHaveAttribute('aria-live', 'polite');
  });

  it('opens the dropdown with the latest items and a See all link', async () => {
    const user = userEvent.setup();
    renderBell();
    await waitFor(() => expect(screen.getByTestId('notification-bell-badge')).toBeInTheDocument());

    await user.click(screen.getByTestId('notification-bell'));
    expect(await screen.findByText('Meeting scheduled')).toBeInTheDocument();
    // The dropdown items are `role="menuitem"` links, not plain `link`s.
    expect(screen.getByRole('menuitem', { name: 'See all' })).toHaveAttribute('href', '/notifications');
  });

  it('marks an item read when clicked and refreshes the count', async () => {
    const user = userEvent.setup();
    renderBell();
    await waitFor(() => expect(screen.getByTestId('notification-bell-badge')).toBeInTheDocument());

    await user.click(screen.getByTestId('notification-bell'));
    await screen.findByTestId('notification-bell-item-1');
    await user.click(screen.getByTestId('notification-bell-item-1'));

    await waitFor(() => expect(notificationsApi.markNotificationRead).toHaveBeenCalledWith(1));
  });

  it('focuses the first item on open and closes on Escape, returning focus to the trigger', async () => {
    const user = userEvent.setup();
    renderBell();
    await waitFor(() => expect(screen.getByTestId('notification-bell-badge')).toBeInTheDocument());

    await user.click(screen.getByTestId('notification-bell'));
    const firstItem = await screen.findByTestId('notification-bell-item-1');
    await waitFor(() => expect(firstItem).toHaveFocus());

    await user.keyboard('{Escape}');
    expect(screen.queryByTestId('notification-bell-menu')).not.toBeInTheDocument();
    expect(screen.getByTestId('notification-bell')).toHaveFocus();
  });

  it('moves focus between items with arrow keys', async () => {
    const user = userEvent.setup();
    renderBell();
    await waitFor(() => expect(screen.getByTestId('notification-bell-badge')).toBeInTheDocument());

    await user.click(screen.getByTestId('notification-bell'));
    await screen.findByTestId('notification-bell-item-1');
    await waitFor(() => expect(screen.getByTestId('notification-bell-item-1')).toHaveFocus());

    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'See all' })).toHaveFocus();
  });

  describe('polling', () => {
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
    });

    afterEach(() => {
      act(() => {
        vi.runOnlyPendingTimers();
      });
      vi.useRealTimers();
    });

    it('polls again after the interval elapses', async () => {
      renderBell();
      await waitFor(() => expect(notificationsApi.listNotifications).toHaveBeenCalledTimes(1));

      await act(async () => {
        vi.advanceTimersByTime(60_000);
      });
      await waitFor(() => expect(notificationsApi.listNotifications).toHaveBeenCalledTimes(2));
    });

    it('skips a poll while the tab is hidden', async () => {
      renderBell();
      await waitFor(() => expect(notificationsApi.listNotifications).toHaveBeenCalledTimes(1));

      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
      await act(async () => {
        vi.advanceTimersByTime(60_000);
      });
      expect(notificationsApi.listNotifications).toHaveBeenCalledTimes(1);

      Object.defineProperty(document, 'hidden', { value: false, configurable: true });
      await act(async () => {
        document.dispatchEvent(new Event('visibilitychange'));
      });
      await waitFor(() => expect(notificationsApi.listNotifications).toHaveBeenCalledTimes(2));
    });
  });
});
