import { AxiosError, AxiosHeaders } from 'axios';

/**
 * An `AxiosError` shaped like a real 4xx from the API: the controller's
 * `ApiResponse` envelope sits in `response.data`. Use with `mockRejectedValue`
 * so tests exercise the same path the runtime client produces.
 */
export function apiRejection(message: string, status = 400): AxiosError {
  const config = { headers: new AxiosHeaders() };
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, undefined, {
    status,
    statusText: 'Error',
    headers: {},
    config,
    data: { success: false, message },
  });
}
