import * as Sentry from '@sentry/react';
import { AppProvider } from './provider';
import { AppRouter } from './routes';

function FallbackError() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-brand-slate-50 px-4">
      <div className="bg-white rounded-card border-[0.5px] border-brand-slate-200 p-8 max-w-md text-center">
        <h1 className="font-serif text-2xl text-brand-slate-800 mb-2">Something went wrong</h1>
        <p className="text-sm text-brand-slate-400 mb-4">
          An unexpected error occurred. Our team has been notified.
        </p>
        <button
          onClick={() => window.location.href = '/dashboard'}
          className="px-4 py-2 bg-brand-teal-500 hover:bg-brand-teal-600 text-white rounded-button text-sm transition-colors"
        >
          Go to Dashboard
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
