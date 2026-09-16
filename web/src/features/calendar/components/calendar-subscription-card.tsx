import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { getCalendarFeed, regenerateCalendarFeed } from '../api/calendar-api';

/** Profile page card: the viewer's personal ICS feed URL, with copy and a
 * confirmed regenerate (which revokes the old link). */
export function CalendarSubscriptionCard() {
  const { show: showToast } = useToast();
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [regenerateError, setRegenerateError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getCalendarFeed();
        if (!active) return;
        if (response.success && response.data) setUrl(response.data.url);
        else setError(response.message ?? 'Could not load your calendar link');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load your calendar link'));
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const handleCopy = async () => {
    if (!url) return;
    try {
      await navigator.clipboard.writeText(url);
      showToast({ message: 'Calendar link copied', variant: 'success' });
    } catch {
      showToast({ message: 'Could not copy the link', variant: 'error' });
    }
  };

  const handleRegenerate = async () => {
    setRegenerating(true);
    setRegenerateError(null);
    try {
      const response = await regenerateCalendarFeed();
      if (response.success && response.data) {
        setUrl(response.data.url);
        setConfirmOpen(false);
        showToast({ message: 'Calendar link regenerated', variant: 'success' });
      } else {
        setRegenerateError(response.message ?? 'Could not regenerate the link');
      }
    } catch (err) {
      setRegenerateError(apiErrorMessage(err, 'Could not regenerate the link'));
    } finally {
      setRegenerating(false);
    }
  };

  return (
    <Card className="max-w-lg" data-testid="calendar-subscription-card">
      <h2 className="mb-2 text-lg font-serif font-semibold text-brand-slate-800">Calendar subscription</h2>
      <p className="mb-3 text-sm text-brand-slate-500">
        Subscribe from Google Calendar, Outlook, or Apple Calendar to see your meetings and deadlines.
      </p>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      {!error && (
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
          <Input
            id="calendar-feed-url"
            label="Feed URL"
            value={url ?? 'Loading…'}
            readOnly
            data-testid="calendar-feed-url"
            className="flex-1"
          />
          <div className="flex shrink-0 gap-2">
            <Button variant="secondary" onClick={handleCopy} disabled={!url} data-testid="calendar-feed-copy">
              Copy
            </Button>
            <Button
              variant="ghost"
              onClick={() => setConfirmOpen(true)}
              disabled={!url}
              data-testid="calendar-feed-regenerate-open"
            >
              Regenerate
            </Button>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={confirmOpen}
        title="Regenerate calendar link"
        message="Anyone using the old link will stop receiving updates. This cannot be undone."
        confirmLabel="Regenerate link"
        confirmVariant="danger"
        loading={regenerating}
        error={regenerateError}
        onConfirm={handleRegenerate}
        onCancel={() => {
          setRegenerateError(null);
          setConfirmOpen(false);
        }}
        data-testid="calendar-feed-regenerate-dialog"
      />
    </Card>
  );
}
