import { apiClient } from '@/lib/api-client';
import { getToken, removeToken } from '@/lib/auth';
import type { ApiResponse } from '@/types/api';
import type {
  AdvocateDoneFrame,
  AdvocateErrorFrame,
  AdvocateThreadDetailDto,
  AdvocateThreadDto,
  AdvocateToolFrame,
  AdvocateUsageDto,
  SendAdvocateMessageRequest,
} from '../types/advocate';
import { parseSseStream } from './sse';

// ---- Threads / usage (plain JSON via the shared axios client) ----

/** Only the caller's own threads, newest `lastMessageAt` first. */
export async function listAdvocateThreads(childId: number): Promise<ApiResponse<AdvocateThreadDto[]>> {
  const res = await apiClient.get<ApiResponse<AdvocateThreadDto[]>>(`/api/children/${childId}/advocate/threads`);
  return res.data;
}

export async function createAdvocateThread(childId: number, title?: string): Promise<ApiResponse<AdvocateThreadDto>> {
  const res = await apiClient.post<ApiResponse<AdvocateThreadDto>>(
    `/api/children/${childId}/advocate/threads`,
    title ? { title } : {},
  );
  return res.data;
}

export async function getAdvocateThread(threadId: number): Promise<ApiResponse<AdvocateThreadDetailDto>> {
  const res = await apiClient.get<ApiResponse<AdvocateThreadDetailDto>>(`/api/advocate/threads/${threadId}`);
  return res.data;
}

export async function renameAdvocateThread(threadId: number, title: string): Promise<ApiResponse<unknown>> {
  const res = await apiClient.patch<ApiResponse<unknown>>(`/api/advocate/threads/${threadId}`, { title });
  return res.data;
}

export async function deleteAdvocateThread(threadId: number): Promise<ApiResponse<unknown>> {
  const res = await apiClient.delete<ApiResponse<unknown>>(`/api/advocate/threads/${threadId}`);
  return res.data;
}

export async function getAdvocateUsage(): Promise<ApiResponse<AdvocateUsageDto>> {
  const res = await apiClient.get<ApiResponse<AdvocateUsageDto>>('/api/advocate/usage');
  return res.data;
}

// ---- Streaming send (fetch + SSE) ----

/** Why a send failed before any answer streamed. */
export type AdvocateRequestErrorCode =
  'validation' | 'forbidden' | 'not_found' | 'usage_cap' | 'rate_limited' | 'unauthorized' | 'unavailable';

/** A non-2xx reply to the message POST — the stream never opened. */
export class AdvocateRequestError extends Error {
  readonly status: number;
  readonly code: AdvocateRequestErrorCode;

  constructor(status: number, code: AdvocateRequestErrorCode, message: string) {
    super(message);
    this.name = 'AdvocateRequestError';
    this.status = status;
    this.code = code;
  }
}

export interface StreamAdvocateMessageHandlers {
  onDelta: (text: string) => void;
  onTool: (frame: AdvocateToolFrame) => void;
  onDone: (frame: AdvocateDoneFrame) => void;
  /** A mid-stream failure (`error` frame, or the stream closing without `done`). */
  onError: (frame: AdvocateErrorFrame) => void;
  signal?: AbortSignal;
}

export const ADVOCATE_UNAVAILABLE_MESSAGE = 'The advocate could not answer right now. Please try again in a moment.';
const RATE_LIMITED_MESSAGE = "You're sending messages quickly — wait a moment and try again.";

/**
 * The body of a failed POST. Validation failures may arrive either as our
 * `ApiResponse` envelope or as ASP.NET's `ValidationProblemDetails`.
 */
function failureMessage(body: unknown, fallback: string): string {
  if (!body || typeof body !== 'object') return fallback;
  const record = body as {
    message?: unknown;
    errors?: unknown;
    title?: unknown;
  };
  if (typeof record.message === 'string' && record.message) return record.message;
  if (Array.isArray(record.errors)) {
    const first = record.errors.find((e) => typeof e === 'string');
    if (first) return first;
  } else if (record.errors && typeof record.errors === 'object') {
    for (const value of Object.values(record.errors as Record<string, unknown>)) {
      if (Array.isArray(value) && typeof value[0] === 'string') return value[0];
    }
  }
  if (typeof record.title === 'string' && record.title) return record.title;
  return fallback;
}

async function readFailureBody(response: Response): Promise<unknown> {
  try {
    const text = await response.text();
    return text ? (JSON.parse(text) as unknown) : null;
  } catch {
    return null;
  }
}

async function toRequestError(response: Response): Promise<AdvocateRequestError> {
  const body = await readFailureBody(response);
  switch (response.status) {
    case 400:
      return new AdvocateRequestError(400, 'validation', failureMessage(body, 'That message could not be sent.'));
    case 403:
      return new AdvocateRequestError(403, 'forbidden', failureMessage(body, "You can't ask the advocate about this child."));
    case 404:
      return new AdvocateRequestError(404, 'not_found', failureMessage(body, 'This conversation is no longer available.'));
    case 429:
      // The usage cap answers with our envelope and a message; the endpoint
      // rate limiter answers with an empty body.
      return body && typeof body === 'object' && 'message' in body
        ? new AdvocateRequestError(429, 'usage_cap', failureMessage(body, "You've used all of this year's advocate messages."))
        : new AdvocateRequestError(429, 'rate_limited', RATE_LIMITED_MESSAGE);
    default:
      return new AdvocateRequestError(response.status, 'unavailable', failureMessage(body, ADVOCATE_UNAVAILABLE_MESSAGE));
  }
}

function parseFrame<T>(data: string): T | null {
  try {
    return JSON.parse(data) as T;
  } catch {
    return null;
  }
}

/**
 * POSTs one message and streams the answer. Resolves once the stream has
 * closed (after `onDone` or `onError`). Rejects with `AdvocateRequestError`
 * for a pre-stream refusal, with the fetch's `AbortError` when `signal`
 * fires, or with the underlying network error.
 *
 * A 401 mirrors the axios interceptor: the token is dropped and the app goes
 * to the login page.
 */
export async function streamAdvocateMessage(
  threadId: number,
  body: SendAdvocateMessageRequest,
  { onDelta, onTool, onDone, onError, signal }: StreamAdvocateMessageHandlers,
): Promise<void> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    Accept: 'text/event-stream',
  };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const response = await fetch(`/api/advocate/threads/${threadId}/messages`, {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
    signal,
  });

  if (response.status === 401) {
    removeToken();
    window.location.href = '/login';
    throw new AdvocateRequestError(401, 'unauthorized', 'Please sign in again.');
  }
  if (!response.ok) throw await toRequestError(response);
  if (!response.body) {
    onError({ code: 'unavailable', message: ADVOCATE_UNAVAILABLE_MESSAGE });
    return;
  }

  let settled = false;
  const reader = response.body.getReader();
  // After a terminal frame nothing else is read; cancelling makes the next
  // `read()` resolve as done so the parser returns without waiting for the
  // server to close the connection.
  const settle = () => {
    settled = true;
    reader.cancel().catch(() => {});
  };
  await parseSseStream(reader, ({ event, data }) => {
    if (settled) return;
    switch (event) {
      case 'delta': {
        const frame = parseFrame<{ text?: unknown }>(data);
        if (frame && typeof frame.text === 'string') onDelta(frame.text);
        break;
      }
      case 'tool': {
        const frame = parseFrame<AdvocateToolFrame>(data);
        if (frame && typeof frame.label === 'string') onTool(frame);
        break;
      }
      case 'done': {
        const frame = parseFrame<AdvocateDoneFrame>(data);
        if (!frame) break;
        settle();
        onDone({
          messageId: frame.messageId,
          contentMarkdown: frame.contentMarkdown ?? '',
          citations: Array.isArray(frame.citations) ? frame.citations : [],
          suggestions: Array.isArray(frame.suggestions) ? frame.suggestions : [],
          truncated: Boolean(frame.truncated),
          disclaimer: frame.disclaimer ?? '',
        });
        break;
      }
      case 'error': {
        const frame = parseFrame<AdvocateErrorFrame>(data);
        settle();
        onError({
          code: frame?.code ?? 'unavailable',
          message: frame?.message || ADVOCATE_UNAVAILABLE_MESSAGE,
        });
        break;
      }
      default:
        break;
    }
  });

  // The connection dropped without a terminal frame — treat it like the server's own `error`.
  if (!settled) onError({ code: 'unavailable', message: ADVOCATE_UNAVAILABLE_MESSAGE });
}
