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

  return event;
}
