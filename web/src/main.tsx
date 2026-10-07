import React from 'react';
import ReactDOM from 'react-dom/client';
import * as Sentry from '@sentry/react';
import { App } from '@/app';
import { installPreloadErrorReload } from '@/lib/preload-error-reload';
import { scrubSentryBreadcrumb, scrubSentryEvent } from '@/lib/sentry-scrub';
import './index.css';

// See `lib/preload-error-reload.ts`'s doc comment: recovers from a lazy
// route chunk failing to load (typically a stale tab after a deploy) with a
// single automatic reload, falling back to `MainLayout`'s route-level error
// boundary if the problem persists.
installPreloadErrorReload();

// Initialize Sentry for frontend error tracking
const sentryDsn = import.meta.env.VITE_SENTRY_DSN;
if (sentryDsn) {
  Sentry.init({
    dsn: sentryDsn,
    environment: import.meta.env.MODE,
    integrations: [
      Sentry.browserTracingIntegration(),
    ],
    tracesSampleRate: 0.1, // 10% of transactions for performance monitoring
    replaysOnErrorSampleRate: 1.0, // Capture replay on 100% of errors
    // Pilot-gates plan, phase 2, decision 4: never send PII by default, and
    // scrub request bodies / auth headers / user email-username-IP from
    // whatever does get sent (see `sentry-scrub.ts`).
    sendDefaultPii: false,
    beforeSend: scrubSentryEvent,
    // Navigation/fetch breadcrumbs carry URLs; one-time tokens ride in query strings.
    beforeBreadcrumb: scrubSentryBreadcrumb,
  });
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
