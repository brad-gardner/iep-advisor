import { describe, expect, it } from 'vitest';
import { parseSseStream, type SseEvent } from './sse';

const encoder = new TextEncoder();

/** A reader that hands out the given chunks one `read()` at a time. */
function readerOf(chunks: string[]): ReadableStreamDefaultReader<Uint8Array> {
  const queue = chunks.map((c) => encoder.encode(c));
  return new ReadableStream<Uint8Array>({
    pull(controller) {
      const next = queue.shift();
      if (next) controller.enqueue(next);
      else controller.close();
    },
  }).getReader();
}

async function collect(chunks: string[]): Promise<SseEvent[]> {
  const events: SseEvent[] = [];
  await parseSseStream(readerOf(chunks), (e) => events.push(e));
  return events;
}

describe('parseSseStream', () => {
  it('dispatches each frame with its event name and data', async () => {
    const events = await collect(['event: delta\ndata: {"text":"Hi"}\n\nevent: done\ndata: {"messageId":1}\n\n']);
    expect(events).toEqual([
      { event: 'delta', data: '{"text":"Hi"}' },
      { event: 'done', data: '{"messageId":1}' },
    ]);
  });

  it('reassembles a frame split anywhere across chunks, including inside a multi-byte character', async () => {
    const frame = 'event: delta\ndata: {"text":"café ☕"}\n\n';
    const bytes = encoder.encode(frame);
    // Split in the middle of the 3-byte ☕ and again mid-field-name.
    const cut1 = 3;
    const cut2 = bytes.length - 5;
    const reader = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(bytes.slice(0, cut1));
        controller.enqueue(bytes.slice(cut1, cut2));
        controller.enqueue(bytes.slice(cut2));
        controller.close();
      },
    }).getReader();
    const events: SseEvent[] = [];
    await parseSseStream(reader, (e) => events.push(e));
    expect(events).toEqual([{ event: 'delta', data: '{"text":"café ☕"}' }]);
  });

  it('joins multi-line data with newlines', async () => {
    const events = await collect(['event: delta\ndata: line one\ndata: line two\ndata:\n\n']);
    expect(events).toEqual([{ event: 'delta', data: 'line one\nline two\n' }]);
  });

  it('ignores comment (ping) lines and id/retry fields', async () => {
    const events = await collect([
      ': ping\n\n',
      'id: 7\nretry: 1000\n: mid-frame comment\nevent: delta\ndata: x\n\n',
      ': ping\n\n',
    ]);
    expect(events).toEqual([{ event: 'delta', data: 'x' }]);
  });

  it('accepts CRLF line endings, even when the CR and LF land in different chunks', async () => {
    const events = await collect(['event: delta\r\ndata: a\r', '\n\r\nevent: done\r\ndata: b\r\n\r\n']);
    expect(events).toEqual([
      { event: 'delta', data: 'a' },
      { event: 'done', data: 'b' },
    ]);
  });

  it('defaults the event name to "message" and tolerates a missing space after the colon', async () => {
    const events = await collect(['data:{"a":1}\n\n']);
    expect(events).toEqual([{ event: 'message', data: '{"a":1}' }]);
  });

  it('dispatches a trailing frame that the stream closed without a blank line', async () => {
    const events = await collect(['event: done\ndata: {"messageId":2}']);
    expect(events).toEqual([{ event: 'done', data: '{"messageId":2}' }]);
  });

  it('discards a frame with no data lines', async () => {
    const events = await collect(['event: delta\n\n', 'event: done\ndata: ok\n\n']);
    expect(events).toEqual([{ event: 'done', data: 'ok' }]);
  });

  it('rejects with the reader error when the fetch is aborted mid-stream', async () => {
    const abort = new DOMException('The operation was aborted.', 'AbortError');
    let reads = 0;
    const reader = {
      read: () => {
        reads += 1;
        return reads === 1
          ? Promise.resolve({ done: false as const, value: encoder.encode('event: delta\ndata: partial\n\n') })
          : Promise.reject(abort);
      },
      cancel: () => Promise.resolve(),
      releaseLock: () => {},
      closed: Promise.resolve(undefined),
    } as unknown as ReadableStreamDefaultReader<Uint8Array>;

    const events: SseEvent[] = [];
    await expect(parseSseStream(reader, (e) => events.push(e))).rejects.toBe(abort);
    expect(events).toEqual([{ event: 'delta', data: 'partial' }]);
  });
});
