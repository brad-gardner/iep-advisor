import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { getCalendarFeed, regenerateCalendarFeed } from '../api/calendar-api';

/**
 * Profile page card: the viewer's personal ICS feed URL, with copy and a
 * confirmed regenerate (which revokes the old link). Reachable by every
 * audience (parent, staff, admin) via the shared `features/auth` Profile
 * page, NOT staff-only — so, unlike the rest of `features/calendar`, the
 * `calendar` namespace stays a normal EAGER namespace
 * (`locales/en/calendar.json`, not `locales/en/staff/`): this card's English
 * must be present before any staff route chunk could register it. See
 * `docs/i18n/README.md`'s "Staff and admin namespaces" for the rule this is
 * the deliberate exception to.
 */
export function CalendarSubscriptionCard() {
  const { t } = useTranslation(['calendar', 'common']);
  const { show: showToast } = useToast();
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [regenerateError, setRegenerateError] = useState<string | null>(null);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getCalendarFeed();
        if (!active) return;
        if (response.success && response.data) {
          setUrl(response.data.url);
          setError(null);
        } else {
          setError(response.message ?? t('subscriptionCard.loadFailed'));
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, t('subscriptionCard.loadFailed')));
      }
    })();
    return () => {
      active = false;
    };
    // `t` omitted deliberately (see `docs/i18n/README.md`'s "An effect that
    // fetches on mount never has `t` in its dependency array") — only
    // `retryToken` should re-run this fetch.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [retryToken]);

  const handleCopy = async () => {
    if (!url) return;
    try {
      await navigator.clipboard.writeText(url);
      showToast({ message: t('subscriptionCard.copySuccess'), variant: 'success' });
    } catch {
      showToast({ message: t('subscriptionCard.copyFailed'), variant: 'error' });
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
        showToast({ message: t('subscriptionCard.regenerateSuccess'), variant: 'success' });
      } else {
        setRegenerateError(response.message ?? t('subscriptionCard.regenerateFailed'));
      }
    } catch (err) {
      setRegenerateError(apiErrorMessage(err, t('subscriptionCard.regenerateFailed')));
    } finally {
      setRegenerating(false);
    }
  };

  return (
    <Card className="max-w-lg" data-testid="calendar-subscription-card">
      <h2 className="mb-2 text-lg font-serif font-semibold text-brand-slate-800">
        {t('subscriptionCard.title')}
      </h2>
      <p className="mb-3 text-sm text-brand-slate-500">{t('subscriptionCard.description')}</p>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((n) => n + 1)}>
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {!error && (
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
          <Input
            id="calendar-feed-url"
            label={t('subscriptionCard.feedUrlLabel')}
            value={url ?? t('common:ui.loading')}
            readOnly
            data-testid="calendar-feed-url"
            className="flex-1"
          />
          <div className="flex shrink-0 gap-2">
            <Button variant="secondary" onClick={handleCopy} disabled={!url} data-testid="calendar-feed-copy">
              {t('subscriptionCard.copy')}
            </Button>
            <Button
              variant="ghost"
              onClick={() => setConfirmOpen(true)}
              disabled={!url}
              data-testid="calendar-feed-regenerate-open"
            >
              {t('subscriptionCard.regenerate')}
            </Button>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={confirmOpen}
        title={t('subscriptionCard.regenerateDialogTitle')}
        message={t('subscriptionCard.regenerateDialogMessage')}
        confirmLabel={t('subscriptionCard.regenerateConfirmLabel')}
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
