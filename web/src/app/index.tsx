import { useEffect } from 'react';
import * as Sentry from '@sentry/react';
import i18n from '@/lib/i18n';
import { AppProvider } from './provider';
import { AppRouter } from './routes';

function FallbackError() {
  // Mirrors `lib/i18n/index.ts`'s own `languageChanged` listener: if i18next
  // never finished initializing, `resolvedLanguage` is undefined and we fall
  // back to "en" — which is also what `index.html` sets statically, so this
  // never regresses the common case.
  useEffect(() => {
    document.documentElement.lang = i18n.resolvedLanguage ?? 'en';
  }, []);

  // This boundary sits ABOVE `AppProvider`, so it must render sensibly even
  // if i18next's own init never finished (e.g. its Spanish backend never
  // resolved) — `i18n.t`'s `defaultValue` covers that: `common` is bundled
  // eagerly and available synchronously for English, but a translated string
  // is never required for this fallback to show something correct.
  return (
    <div className="min-h-screen flex items-center justify-center bg-brand-slate-50 px-4">
      <div className="bg-white rounded-card border border-brand-slate-200 p-8 max-w-md text-center">
        <h1 className="font-serif text-2xl text-brand-slate-800 mb-2">
          {i18n.t('common:rootError.title', { defaultValue: 'Something went wrong' })}
        </h1>
        <p className="text-sm text-brand-slate-500 mb-4">
          {i18n.t('common:rootError.body', {
            defaultValue: 'An unexpected error occurred. Our team has been notified.',
          })}
        </p>
        <button
          onClick={() => (window.location.href = '/dashboard')}
          className="px-4 py-2 bg-brand-teal-500 hover:bg-brand-teal-600 text-white rounded-button text-sm transition-colors"
        >
          {i18n.t('common:rootError.goToDashboard', { defaultValue: 'Go to Dashboard' })}
        </button>
      </div>
    </div>
  );
}

export function App() {
  return (
    <Sentry.ErrorBoundary fallback={<FallbackError />}>
      <AppProvider>
        <AppRouter />
      </AppProvider>
    </Sentry.ErrorBoundary>
  );
}
