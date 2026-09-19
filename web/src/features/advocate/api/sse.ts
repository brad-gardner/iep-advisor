/**
 * A small server-sent-events parser over a `fetch` body reader. `EventSource`
 * cannot send the Bearer header this API needs, so the advocate stream is read
 * by hand. Follows the WHATWG event-stream grammar closely enough for our own
 * server (`SseWriter`): `event:` / `data:` fields, `:` comment lines (the
 * server's 15 s pings), CRLF or LF line endings, multi-line `data:` joined with
 * `\n`, frames split anywhere across chunks, and `id:` / `retry:` ignored.
 *
 * Resolves when the stream ends; rejects if `reader.read()` rejects (an
 * `AbortError` when the caller aborts the fetch). A trailing frame with no
 * closing blank line is still dispatched at end of stream.
 */

export interface SseEvent {
  /** `event:` field, or `message` when the frame had none. */
  event: string;
  /** Concatenated `data:` lines. */
  data: string;
}

export type SseEventHandler = (event: SseEvent) => void;

interface FrameState {
  event: string | null;
  data: string[];
}

function emptyFrame(): FrameState {
  return { event: null, data: [] };
}

function dispatch(frame: FrameState, onEvent: SseEventHandler): void {
  // Per spec a frame with no data lines is discarded (a bare `event:` or a
  // comment-only ping never reaches the consumer).
  if (frame.data.length === 0) return;
  onEvent({ event: frame.event ?? 'message', data: frame.data.join('\n') });
}

/** Feeds one complete line into the frame; returns true when the line closed the frame. */
function consumeLine(line: string, frame: FrameState): boolean {
  if (line === '') return true;
  if (line.startsWith(':')) return false;

  const colon = line.indexOf(':');
  const field = colon === -1 ? line : line.slice(0, colon);
  let value = colon === -1 ? '' : line.slice(colon + 1);
  if (value.startsWith(' ')) value = value.slice(1);

  if (field === 'event') frame.event = value;
  else if (field === 'data') frame.data.push(value);
  // `id`, `retry` and unknown fields are ignored.
  return false;
}

export async function parseSseStream(reader: ReadableStreamDefaultReader<Uint8Array>, onEvent: SseEventHandler): Promise<void> {
  const decoder = new TextDecoder('utf-8');
  let buffer = '';
  let frame = emptyFrame();

  const drain = (final: boolean) => {
    // Normalise CRLF / lone CR to LF once, then split on complete lines only:
    // whatever follows the last newline may be half a line and waits for the
    // next chunk. A trailing lone `\r` is kept back too — it may be the first
    // half of a CRLF pair straddling a chunk boundary.
    let text = buffer.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    if (!final && buffer.endsWith('\r')) text = text.slice(0, -1) + '\r';
    const lastNewline = text.lastIndexOf('\n');
    const complete = final ? text : lastNewline === -1 ? '' : text.slice(0, lastNewline + 1);
    buffer = final ? '' : text.slice(lastNewline + 1);
    if (!complete) return;

    const lines = complete.split('\n');
    // A complete block always ends with '\n', so the final split entry is the
    // empty remainder after it, not a line — unless this is the final drain
    // of a stream whose last line had no newline.
    if (!final || complete.endsWith('\n')) lines.pop();
    for (const line of lines) {
      if (consumeLine(line, frame)) {
        dispatch(frame, onEvent);
        frame = emptyFrame();
      }
    }
  };

  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    drain(false);
  }
  buffer += decoder.decode();
  drain(true);
  dispatch(frame, onEvent);
}
