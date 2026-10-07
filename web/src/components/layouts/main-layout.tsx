import { useNavigate } from 'react-router-dom';
import * as Sentry from '@sentry/react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { NotificationsProvider } from '@/features/notifications/stores/notifications-context';
import { cn } from '@/lib/cn';
import { Sidebar } from './sidebar';

interface MainLayoutProps {
  children: React.ReactNode;
  /** Widens the content column beyond the default `max-w-7xl` — for a route
   *  whose content (e.g. a two-column editor with its own nav rail) needs more
   *  room than the standard page shell affords. Keep this a one-file, opt-in
   *  knob rather than adding more width tiers than a route actually needs. */
  wide?: boolean;
}

// A route-level fallback for the `Sentry.ErrorBoundary` below — deliberately
// small and in-page (not full-screen like `app/index.tsx`'s top-level one),
// since the sidebar/header chrome around it is still intact and usable. The
// most common cause this catches is a lazy route chunk (`React.lazy`) that
// fails to load — a stale cached HTML pointing at a chunk hash a newer
// deploy removed, or a dropped connection — which React surfaces as a
// render-time throw from inside the `Suspense` boundary the route wraps its
// lazy page in (`app/routes.tsx`), not as a promise rejection this boundary
// could otherwise ignore. `main.tsx`'s `vite:preloadError` listener already
// retries once, automatically, before this ever renders; reaching here means
// that retry didn't happen (a different render error) or already happened
// once and the problem persists, so the only further remediation offered is
// a manual reload rather than `resetError()` — `React.lazy`'s rejected
// import promise is cached for the component's lifetime, so re-rendering the
// same tree without a full reload would just throw again.
function LazyRouteErrorFallback() {
  const { t } = useTranslation('common');
  return (
    <div
      className="flex flex-col items-center gap-3 py-12 text-center"
      role="alert"
      data-testid="lazy-route-error"
    >
      <p className="text-sm text-brand-slate-600">{t('ui.genericError')}</p>
      <Button variant="secondary" size="sm" onClick={() => window.location.reload()}>
        {t('ui.reload')}
      </Button>
    </div>
  );
}

export function MainLayout({ children, wide }: MainLayoutProps) {
  const { logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    // One shared unread-notifications poll for everything MainLayout renders
    // — the sidebar bell (mounted twice: mobile + desktop) and any page
    // content (e.g. the full Notifications page) — rather than each bell
    // instance racing its own independent fetch. Scoped here (not the app
    // root) so it never polls on public/unauthenticated routes.
    <NotificationsProvider>
      <div className="min-h-screen bg-brand-slate-50">
        <Sidebar onLogout={handleLogout} />

        <main className="md:ml-64">
          <div className={cn('mx-auto px-4 sm:px-6 lg:px-8 py-8 pt-16 md:pt-8', wide ? 'max-w-[1400px]' : 'max-w-7xl')}>
            <Sentry.ErrorBoundary fallback={<LazyRouteErrorFallback />}>{children}</Sentry.ErrorBoundary>
          </div>
        </main>
      </div>
    </NotificationsProvider>
  );
}
