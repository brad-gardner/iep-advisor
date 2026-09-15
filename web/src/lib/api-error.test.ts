import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { apiErrorMessage } from './api-error';

function axiosRejection(status: number, data: unknown): AxiosError {
  const config = { headers: new AxiosHeaders() };
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, undefined, {
    status,
    statusText: 'Bad Request',
    headers: {},
    config,
    data,
  });
}

describe('apiErrorMessage', () => {
  it('surfaces the envelope message from a 4xx rejection', () => {
    const err = axiosRejection(400, { success: false, message: 'Choose a new lead case manager first.' });
    expect(apiErrorMessage(err, 'fallback')).toBe('Choose a new lead case manager first.');
  });

  it('falls back when the body carries no message (ProblemDetails, empty body)', () => {
    expect(apiErrorMessage(axiosRejection(400, { title: 'Bad Request' }), 'fallback')).toBe('fallback');
    expect(apiErrorMessage(axiosRejection(500, ''), 'fallback')).toBe('fallback');
  });

  it('falls back for non-axios errors', () => {
    expect(apiErrorMessage(new Error('boom'), 'fallback')).toBe('fallback');
    expect(apiErrorMessage(undefined, 'fallback')).toBe('fallback');
  });
});
