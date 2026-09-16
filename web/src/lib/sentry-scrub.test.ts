import { describe, expect, it } from 'vitest';
import { scrubSentryEvent } from './sentry-scrub';

describe('scrubSentryEvent', () => {
  it('drops the request body and cookies', () => {
    const event = {
      request: {
        data: { note: 'a parent wrote something private' },
        cookies: { session: 'abc123' },
        url: 'https://api.example.com/foo',
      },
    };

    const result = scrubSentryEvent(event);

    expect(result.request?.data).toBeUndefined();
    expect(result.request?.cookies).toBeUndefined();
    expect(result.request?.url).toBe('https://api.example.com/foo');
  });

  it('strips Authorization and Cookie headers case-insensitively, keeping the rest', () => {
    const event = {
      request: {
        headers: {
          Authorization: 'Bearer secret-token',
          cookie: 'session=abc123',
          COOKIE: 'other=1',
          'Content-Type': 'application/json',
        },
      },
    };

    const result = scrubSentryEvent(event);

    expect(result.request?.headers).toEqual({ 'Content-Type': 'application/json' });
  });

  it('removes email, username and ip_address from the user context but keeps the numeric id', () => {
    const event = {
      user: {
        id: 42,
        email: 'parent@example.com',
        username: 'parent42',
        ip_address: '203.0.113.5',
      },
    };

    const result = scrubSentryEvent(event);

    expect(result.user).toEqual({ id: 42 });
  });

  it('is a no-op when request/user are absent', () => {
    expect(scrubSentryEvent({})).toEqual({});
  });

  it('returns the same object reference (mutates in place)', () => {
    const event = { user: { id: 1, email: 'a@b.com' } };
    expect(scrubSentryEvent(event)).toBe(event);
  });
});
