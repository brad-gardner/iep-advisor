import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { apiErrorMessage, loadErrorText, toLoadError } from './api-error';

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

describe('toLoadError', () => {
  it('reads the message off a failed ApiResponse', () => {
    expect(toLoadError({ success: false, message: 'Choose a new lead case manager first.' })).toEqual({
      kind: 'server',
      message: 'Choose a new lead case manager first.',
    });
  });

  it('treats a failed ApiResponse with no message as generic', () => {
    expect(toLoadError({ success: false })).toEqual({ kind: 'generic' });
  });

  it('extracts the envelope message from a caught axios rejection', () => {
    const err = axiosRejection(400, { success: false, message: 'District is at capacity.' });
    expect(toLoadError(err)).toEqual({ kind: 'server', message: 'District is at capacity.' });
  });

  it('treats a caught error with no envelope message as generic', () => {
    expect(toLoadError(new Error('boom'))).toEqual({ kind: 'generic' });
    expect(toLoadError(axiosRejection(500, ''))).toEqual({ kind: 'generic' });
  });
});

describe('loadErrorText', () => {
  it('returns null for no error', () => {
    expect(loadErrorText(null, 'fallback')).toBeNull();
  });

  it('shows the server message as-is', () => {
    expect(loadErrorText({ kind: 'server', message: 'District is at capacity.' }, 'fallback')).toBe(
      'District is at capacity.'
    );
  });

  it('shows the fallback for a generic error', () => {
    expect(loadErrorText({ kind: 'generic' }, 'fallback')).toBe('fallback');
  });
});
