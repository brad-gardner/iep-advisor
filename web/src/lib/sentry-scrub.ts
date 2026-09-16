// Pilot-gates plan, phase 2, decision 4: Sentry must never carry a user's PII
// or session credentials. `main.tsx` wires this in as `beforeSend` alongside
// `sendDefaultPii: false`; it is kept in its own module (rather than inlined)
// so it has a real unit test independent of Sentry's own `init()` wiring.
//
// The shape below is a deliberately minimal structural subset of Sentry's own
// `Event` type (just the two pieces this function touches) rather than an
// import from `@sentry/*` — those packages export the type from a deep,
// version-sensitive path, and every field here is optional, so any real
// Sentry `Event` satisfies it structurally.
export interface ScrubbableSentryEvent {
  request?: {
    url?: string;
    data?: unknown;
    cookies?: Record<string, string>;
    headers?: Record<string, string>;
  };
  user?: {
    id?: string | number;
    email?: string;
    username?: string;
    ip_address?: string | null;
  };
  breadcrumbs?: ScrubbableBreadcrumb[];
}

/** The subset of a Sentry `Breadcrumb` this module touches. */
export interface ScrubbableBreadcrumb {
  category?: string;
  message?: string;
  data?: Record<string, unknown>;
}

/** Drops the query string (and fragment) — that is where one-time tokens travel (`/auth/magic?token=…`,
 *  `/account/cancel-deletion?token=…`), and a breadcrumb never needs it. */
export function stripQuery(url: unknown): unknown {
  if (typeof url !== 'string') return url;
  const cut = url.search(/[?#]/);
  return cut === -1 ? url : url.slice(0, cut);
}

const URL_BREADCRUMB_KEYS = ['url', 'to', 'from'] as const;

/**
 * Sentry `beforeBreadcrumb` hook: navigation breadcrumbs record the literal
 * `pushState` argument (path + query), and fetch/xhr breadcrumbs record the
 * request URL — strip query strings from both so a live token never rides
 * along with a later error report.
 */
export function scrubSentryBreadcrumb<T extends ScrubbableBreadcrumb>(breadcrumb: T): T {
  if (breadcrumb.data) {
    for (const key of URL_BREADCRUMB_KEYS) {
      if (key in breadcrumb.data) breadcrumb.data[key] = stripQuery(breadcrumb.data[key]);
    }
  }
  if (typeof breadcrumb.message === 'string' && /[?#]/.test(breadcrumb.message)) {
    breadcrumb.message = breadcrumb.message.replace(/([?#])[^\s]*/g, '');
  }
  return breadcrumb;
}

const SENSITIVE_REQUEST_HEADERS = new Set(['authorization', 'cookie']);

/**
 * Sentry `beforeSend` hook. Mutates and returns the same event:
 *  - Drops the request body outright (`event.request.data`) — a failed
 *    request can carry a parent's note text, a child's data, credentials, etc.
 *  - Drops `event.request.cookies` and strips `Authorization`/`Cookie`
 *    request headers (case-insensitively) so a session token never leaves
 *    the browser via a captured request.
 *  - Keeps only the numeric `id` on the Sentry user context (set by
 *    `AuthProvider` via `Sentry.setUser`) — email/username/IP are removed.
 */
export function scrubSentryEvent<T extends ScrubbableSentryEvent>(event: T): T {
  if (event.request) {
    event.request.url = stripQuery(event.request.url) as string | undefined;
    delete event.request.data;
    delete event.request.cookies;
    if (event.request.headers) {
      const headers: Record<string, string> = {};
      for (const [key, value] of Object.entries(event.request.headers)) {
        if (!SENSITIVE_REQUEST_HEADERS.has(key.toLowerCase())) headers[key] = value;
      }
      event.request.headers = headers;
    }
  }

  if (event.user) {
    delete event.user.email;
    delete event.user.username;
    delete event.user.ip_address;
  }

  // Belt and braces: breadcrumbs attached to the event itself (already filtered by
  // `beforeBreadcrumb` when they were recorded, but a custom integration may add its own).
  if (event.breadcrumbs) {
    for (const crumb of event.breadcrumbs) scrubSentryBreadcrumb(crumb);
  }

  return event;
}
