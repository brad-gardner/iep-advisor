import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const auth = vi.hoisted(() => ({ getToken: vi.fn(() => 'tok-123'), removeToken: vi.fn() }));
vi.mock('@/lib/auth', () => auth);
vi.mock('@/lib/api-client', () => ({ apiClient: { get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn() } }));

import { AdvocateRequestError, streamAdvocateMessage } from './advocate-api';
import type { AdvocateDoneFrame, AdvocateErrorFrame, AdvocateToolFrame } from '../types/advocate';

const encoder = new TextEncoder();

function sseResponse(chunks: string[], init: ResponseInit = {}): Response {
  const queue = chunks.map((c) => encoder.encode(c));
  const body = new ReadableStream<Uint8Array>({
    pull(controller) {
      const next = queue.shift();
      if (next) controller.enqueue(next);
      else controller.close();
    },
  });
  return new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' }, ...init });
}

function handlers() {
  return {
    onDelta: vi.fn<(text: string) => void>(),
    onTool: vi.fn<(f: AdvocateToolFrame) => void>(),
    onDone: vi.fn<(f: AdvocateDoneFrame) => void>(),
    onError: vi.fn<(f: AdvocateErrorFrame) => void>(),
  };
}

describe('streamAdvocateMessage', () => {
  const fetchMock = vi.fn<typeof fetch>();
  let originalLocation: Location;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('fetch', fetchMock);
    originalLocation = window.location;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    Object.defineProperty(window, 'location', { value: originalLocation, writable: true, configurable: true });
  });

  it('POSTs with the Bearer token and relays delta, tool and done frames in order', async () => {
    fetchMock.mockResolvedValue(
      sseResponse([
        'event: tool\ndata: {"name":"search_knowledge_base","label":"Checking the rules","status":"started"}\n\n',
        ': ping\n\n',
        'event: tool\ndata: {"name":"search_knowledge_base","label":"Checking the rules","status":"finished"}\n\n',
        'event: delta\ndata: {"text":"Prior "}\n\nevent: delta\ndata: {"text":"written notice…"}\n\n',
        'event: done\ndata: {"messageId":9,"contentMarkdown":"Prior written notice…","citations":[{"kind":"kb","id":3,"label":"PWN"}],"suggestions":[],"truncated":false,"disclaimer":"Not legal advice."}\n\n',
      ]),
    );
    const h = handlers();
    const controller = new AbortController();

    await streamAdvocateMessage(5, { text: 'What is PWN?', about: 'iep:12' }, { ...h, signal: controller.signal });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/advocate/threads/5/messages');
    expect(init?.method).toBe('POST');
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer tok-123');
    expect((init?.headers as Record<string, string>).Accept).toBe('text/event-stream');
    expect(JSON.parse(init?.body as string)).toEqual({ text: 'What is PWN?', about: 'iep:12' });
    expect(init?.signal).toBe(controller.signal);

    expect(h.onTool.mock.calls.map(([f]) => f.status)).toEqual(['started', 'finished']);
    expect(h.onDelta.mock.calls.map(([t]) => t)).toEqual(['Prior ', 'written notice…']);
    expect(h.onDone).toHaveBeenCalledWith({
      messageId: 9,
      contentMarkdown: 'Prior written notice…',
      citations: [{ kind: 'kb', id: 3, label: 'PWN' }],
      suggestions: [],
      truncated: false,
      disclaimer: 'Not legal advice.',
    });
    expect(h.onError).not.toHaveBeenCalled();
  });

  it('relays a mid-stream error frame after deltas and stops there', async () => {
    fetchMock.mockResolvedValue(
      sseResponse([
        'event: delta\ndata: {"text":"Half an"}\n\n',
        'event: error\ndata: {"code":"unavailable","message":"The advocate could not answer right now."}\n\n',
        'event: delta\ndata: {"text":"ignored"}\n\n',
      ]),
    );
    const h = handlers();
    await streamAdvocateMessage(5, { text: 'x' }, h);
    expect(h.onDelta).toHaveBeenCalledTimes(1);
    expect(h.onError).toHaveBeenCalledWith({ code: 'unavailable', message: 'The advocate could not answer right now.' });
    expect(h.onDone).not.toHaveBeenCalled();
  });

  it('treats a stream that closes without a terminal frame as unavailable', async () => {
    fetchMock.mockResolvedValue(sseResponse(['event: delta\ndata: {"text":"…"}\n\n']));
    const h = handlers();
    await streamAdvocateMessage(5, { text: 'x' }, h);
    expect(h.onError).toHaveBeenCalledWith(expect.objectContaining({ code: 'unavailable' }));
  });

  it('classifies a 429 with our envelope as the usage cap and an empty 429 as rate limiting', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ success: false, message: 'You have used all of this year’s advocate messages.' }), {
        status: 429,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    const capped = await streamAdvocateMessage(5, { text: 'x' }, handlers()).catch((e: unknown) => e);
    expect(capped).toBeInstanceOf(AdvocateRequestError);
    expect(capped).toMatchObject({
      status: 429,
      code: 'usage_cap',
      message: 'You have used all of this year’s advocate messages.',
    });

    fetchMock.mockResolvedValueOnce(new Response(null, { status: 429 }));
    const limited = await streamAdvocateMessage(5, { text: 'x' }, handlers()).catch((e: unknown) => e);
    expect(limited).toMatchObject({ status: 429, code: 'rate_limited' });
  });

  it('reads a message out of either failure body shape for a 400', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          type: 'https://…',
          title: 'One or more validation errors occurred.',
          errors: { Text: ['Text is too long.'] },
        }),
        {
          status: 400,
        },
      ),
    );
    const problem = await streamAdvocateMessage(5, { text: 'x' }, handlers()).catch((e: unknown) => e);
    expect(problem).toMatchObject({ code: 'validation', message: 'Text is too long.' });

    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ success: false, message: 'Bad about.' }), { status: 400 }));
    const envelope = await streamAdvocateMessage(5, { text: 'x' }, handlers()).catch((e: unknown) => e);
    expect(envelope).toMatchObject({ code: 'validation', message: 'Bad about.' });
  });

  it('maps 403 and 404 to their codes', async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ success: false, message: 'No.' }), { status: 403 }));
    await expect(streamAdvocateMessage(5, { text: 'x' }, handlers())).rejects.toMatchObject({
      code: 'forbidden',
      message: 'No.',
    });
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ success: false, message: 'Thread not found' }), { status: 404 }),
    );
    await expect(streamAdvocateMessage(5, { text: 'x' }, handlers())).rejects.toMatchObject({ code: 'not_found' });
  });

  it('mirrors the axios interceptor on 401: drops the token and goes to /login', async () => {
    Object.defineProperty(window, 'location', { value: { href: '/children/4/advocate' }, writable: true, configurable: true });
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 }));
    await expect(streamAdvocateMessage(5, { text: 'x' }, handlers())).rejects.toMatchObject({ code: 'unauthorized' });
    expect(auth.removeToken).toHaveBeenCalled();
    expect(window.location.href).toBe('/login');
  });

  it('propagates the abort rejection from fetch', async () => {
    const abort = new DOMException('aborted', 'AbortError');
    fetchMock.mockRejectedValueOnce(abort);
    await expect(streamAdvocateMessage(5, { text: 'x' }, handlers())).rejects.toBe(abort);
  });
});
