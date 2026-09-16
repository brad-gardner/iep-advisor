import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';

const calendarApi = vi.hoisted(() => ({
  getCalendarFeed: vi.fn(),
  regenerateCalendarFeed: vi.fn(),
}));
vi.mock('../api/calendar-api', () => calendarApi);

import { CalendarSubscriptionCard } from './calendar-subscription-card';

function renderCard() {
  return render(
    <ToastProvider>
      <CalendarSubscriptionCard />
    </ToastProvider>
  );
}

describe('CalendarSubscriptionCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    calendarApi.getCalendarFeed.mockResolvedValue({
      success: true,
      data: { url: 'https://app.example/api/calendar/feed/abc123.ics', createdAt: '2026-01-01T00:00:00Z' },
    });
  });

  it('loads and displays the feed URL', async () => {
    renderCard();
    await waitFor(() =>
      expect(screen.getByTestId('calendar-feed-url')).toHaveValue('https://app.example/api/calendar/feed/abc123.ics')
    );
  });

  it('copies the feed URL to the clipboard', async () => {
    const user = userEvent.setup();
    renderCard();
    await waitFor(() => expect(screen.getByTestId('calendar-feed-copy')).not.toBeDisabled());

    await user.click(screen.getByTestId('calendar-feed-copy'));
    await waitFor(async () => {
      expect(await navigator.clipboard.readText()).toBe('https://app.example/api/calendar/feed/abc123.ics');
    });
  });

  it('regenerates the feed URL after confirming', async () => {
    const user = userEvent.setup();
    calendarApi.regenerateCalendarFeed.mockResolvedValue({
      success: true,
      data: { url: 'https://app.example/api/calendar/feed/newtoken.ics', createdAt: '2026-02-01T00:00:00Z' },
    });
    renderCard();
    await waitFor(() => expect(screen.getByTestId('calendar-feed-regenerate-open')).not.toBeDisabled());

    await user.click(screen.getByTestId('calendar-feed-regenerate-open'));
    await user.click(screen.getByTestId('calendar-feed-regenerate-dialog-confirm'));

    await waitFor(() =>
      expect(screen.getByTestId('calendar-feed-url')).toHaveValue(
        'https://app.example/api/calendar/feed/newtoken.ics'
      )
    );
  });

  it('surfaces a regenerate failure inside the confirm dialog', async () => {
    const user = userEvent.setup();
    calendarApi.regenerateCalendarFeed.mockRejectedValue(apiRejection('Could not regenerate right now.'));
    renderCard();
    await waitFor(() => expect(screen.getByTestId('calendar-feed-regenerate-open')).not.toBeDisabled());

    await user.click(screen.getByTestId('calendar-feed-regenerate-open'));
    await user.click(screen.getByTestId('calendar-feed-regenerate-dialog-confirm'));

    expect(await screen.findByText('Could not regenerate right now.')).toBeInTheDocument();
  });

  it('surfaces a load failure inline', async () => {
    calendarApi.getCalendarFeed.mockRejectedValue(apiRejection('Could not load your calendar link'));
    renderCard();
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load your calendar link');
  });
});
