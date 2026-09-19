import { useCallback, useEffect, useRef, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import {
  ADVOCATE_UNAVAILABLE_MESSAGE,
  AdvocateRequestError,
  getAdvocateThread,
  streamAdvocateMessage,
  type AdvocateRequestErrorCode,
} from '../api/advocate-api';
import type { AdvocateDoneFrame, AdvocateMessageDto, AdvocateToolFrame, AdvocateToolStatus } from '../types/advocate';

export interface ToolActivity {
  key: number;
  name: string;
  label: string;
  status: AdvocateToolStatus;
}

/** The user's message while its answer is in flight or has failed — not yet confirmed from the server. */
export interface PendingMessage {
  threadId: number;
  text: string;
  about?: string;
}

/** The assistant bubble being built from `delta` frames. */
export interface StreamingAnswer {
  text: string;
  tools: ToolActivity[];
}

/**
 * The most recently completed answer for a thread, tracked from `done` so an
 * sr-only status node can announce it once — separately from the (non-live)
 * streaming bubble, which a screen reader would otherwise re-read per token.
 */
export interface AdvocateAnnouncement {
  id: number;
  text: string;
}

export type SendFailureCode = AdvocateRequestErrorCode | 'network';

export interface SendFailure {
  code: SendFailureCode;
  message: string;
  /** Whether "Retry" makes sense — the message was not answered and can be re-sent. */
  retryable: boolean;
}

interface Run {
  threadId: number;
  controller: AbortController;
  /** Set once `done` has arrived; Stop is a no-op from then on. */
  settling: boolean;
}

interface Options {
  /** Called after a `done` frame — the page refreshes the usage counter and bumps the thread rail. */
  onAnswered?: (threadId: number) => void;
  /** Called when a send fails (pre-stream refusal, mid-stream error or network) — the page reacts to the code. */
  onFailure?: (failure: SendFailure, threadId: number) => void;
}

/** Everything the server holds for one thread, tagged so a stale load for another thread is ignored. */
interface LoadedThread {
  threadId: number;
  messages: AdvocateMessageDto[];
  disclaimer: string;
}

interface Keyed<T> {
  threadId: number;
  value: T;
}

const LOAD_ERROR = 'Could not load this conversation.';

/** Codes for which the server never stored the message, so the pending bubble is dropped. */
const DROP_PENDING: ReadonlySet<SendFailureCode> = new Set(['validation', 'forbidden', 'not_found', 'usage_cap', 'unauthorized']);

function isAbortError(err: unknown): boolean {
  return err instanceof DOMException ? err.name === 'AbortError' : (err as { name?: string } | null)?.name === 'AbortError';
}

function toFailure(err: unknown): SendFailure {
  if (err instanceof AdvocateRequestError) {
    return { code: err.code, message: err.message, retryable: err.code === 'rate_limited' || err.code === 'unavailable' };
  }
  return { code: 'network', message: apiErrorMessage(err, ADVOCATE_UNAVAILABLE_MESSAGE), retryable: true };
}

function syntheticUserMessage(text: string): AdvocateMessageDto {
  return {
    id: -Date.now(),
    role: 'User',
    contentMarkdown: text,
    citations: [],
    suggestions: [],
    truncated: false,
    createdAt: new Date().toISOString(),
  };
}

/** The value if it belongs to the open thread, else nothing — how per-thread state is read. */
function forThread<T>(keyed: Keyed<T> | null, threadId: number | null): T | null {
  return keyed && keyed.threadId === threadId ? keyed.value : null;
}

/**
 * One open conversation: its stored messages plus the in-flight send. Every
 * piece of state is tagged with the thread it belongs to and read through
 * `forThread`, so switching threads never needs a reset effect and a late
 * response for another thread cannot leak into this one. The pending user
 * message and the streaming assistant bubble are kept apart from the loaded
 * messages so a (re)load can never clobber them; after `done` the thread is
 * refetched so the list reflects exactly what the server stored, and only
 * then do the local bubbles go away.
 *
 * Every stream is tied to a `Run`; callbacks from a run that is no longer
 * current (aborted by Stop, unmount or a thread switch) are ignored.
 */
export function useAdvocateThread(threadId: number | null, { onAnswered, onFailure }: Options = {}) {
  const [loaded, setLoaded] = useState<LoadedThread | null>(null);
  const [loadFailure, setLoadFailure] = useState<Keyed<string> | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const [pending, setPending] = useState<PendingMessage | null>(null);
  const [streaming, setStreaming] = useState<Keyed<StreamingAnswer> | null>(null);
  const [failure, setFailure] = useState<Keyed<SendFailure> | null>(null);
  const [stoppedIn, setStoppedIn] = useState<number | null>(null);
  const [answered, setAnswered] = useState<Keyed<AdvocateAnnouncement> | null>(null);

  const runRef = useRef<Run | null>(null);
  const onAnsweredRef = useRef(onAnswered);
  const onFailureRef = useRef(onFailure);
  const toolKeyRef = useRef(0);

  useEffect(() => {
    onAnsweredRef.current = onAnswered;
    onFailureRef.current = onFailure;
  });

  const abortRun = useCallback(() => {
    const run = runRef.current;
    runRef.current = null;
    run?.controller.abort();
    return run;
  }, []);

  // Switching threads abandons a stream for another thread (its bubbles are
  // hidden by `forThread`); a stream for *this* thread — started right after
  // the page created it — carries on. Unmount aborts whatever is running.
  // This is the only place a run is dropped without its own settle/error
  // cleanup, so the abandoned run's pending/streaming/failure state is
  // cleared here too — otherwise reopening that thread later would show a
  // frozen streaming bubble, an inert Stop button and a composer that can
  // never send again.
  useEffect(() => {
    if (runRef.current && runRef.current.threadId !== threadId) {
      const abandoned = abortRun();
      if (abandoned) {
        setPending((p) => (p?.threadId === abandoned.threadId ? null : p));
        setStreaming((s) => (s?.threadId === abandoned.threadId ? null : s));
        setFailure((f) => (f?.threadId === abandoned.threadId ? null : f));
      }
    }
  }, [threadId, abortRun]);

  useEffect(
    () => () => {
      abortRun();
    },
    [abortRun],
  );

  useEffect(() => {
    if (threadId == null) return;
    let active = true;
    getAdvocateThread(threadId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) {
          setLoaded({ threadId, messages: res.data.messages, disclaimer: res.data.disclaimer });
          setLoadFailure(null);
        } else {
          setLoadFailure({ threadId, value: res.message ?? LOAD_ERROR });
        }
      })
      .catch((err) => {
        if (active) setLoadFailure({ threadId, value: apiErrorMessage(err, LOAD_ERROR) });
      });
    return () => {
      active = false;
    };
  }, [threadId, reloadToken]);

  const reload = useCallback(() => {
    setLoaded(null);
    setLoadFailure(null);
    setReloadToken((t) => t + 1);
  }, []);

  /** After `done`: refetch so ids/ordering come from the server, then drop the local bubbles. */
  const settleFromServer = useCallback(async (run: Run, message: PendingMessage, done: AdvocateDoneFrame) => {
    let synced = false;
    try {
      const res = await getAdvocateThread(run.threadId);
      if (res.success && res.data) {
        if (runRef.current !== run) return;
        setLoaded({ threadId: run.threadId, messages: res.data.messages, disclaimer: res.data.disclaimer || done.disclaimer });
        synced = true;
      }
    } catch {
      // Fall through to the local append below.
    }
    if (runRef.current !== run) return;
    if (!synced) {
      setLoaded((prev) => ({
        threadId: run.threadId,
        disclaimer: done.disclaimer || prev?.disclaimer || '',
        messages: [
          ...(prev?.threadId === run.threadId ? prev.messages : []),
          syntheticUserMessage(message.text),
          {
            id: done.messageId,
            role: 'Assistant',
            contentMarkdown: done.contentMarkdown,
            citations: done.citations,
            suggestions: done.suggestions,
            truncated: done.truncated,
            createdAt: new Date().toISOString(),
          },
        ],
      }));
    }
    runRef.current = null;
    setPending(null);
    setStreaming(null);
  }, []);

  const start = useCallback(
    (message: PendingMessage) => {
      abortRun();
      const run: Run = { threadId: message.threadId, controller: new AbortController(), settling: false };
      runRef.current = run;
      const id = message.threadId;
      setPending(message);
      setStreaming({ threadId: id, value: { text: '', tools: [] } });
      setFailure(null);
      setStoppedIn(null);

      const current = () => runRef.current === run;
      const patch = (fn: (s: StreamingAnswer) => StreamingAnswer) =>
        setStreaming((prev) => ({ threadId: id, value: fn(prev?.threadId === id ? prev.value : { text: '', tools: [] }) }));

      streamAdvocateMessage(id, message.about ? { text: message.text, about: message.about } : { text: message.text }, {
        signal: run.controller.signal,
        onDelta: (text) => {
          if (!current()) return;
          patch((s) => ({ ...s, text: s.text + text }));
        },
        onTool: (frame: AdvocateToolFrame) => {
          if (!current()) return;
          patch((s) => ({ ...s, tools: applyToolFrame(s.tools, frame, () => (toolKeyRef.current += 1)) }));
        },
        onDone: (done) => {
          if (!current()) return;
          run.settling = true;
          // Show the final text at once; the refetch below only swaps in server ids.
          patch((s) => ({ ...s, text: done.contentMarkdown }));
          setAnswered({ threadId: id, value: { id: done.messageId, text: done.contentMarkdown } });
          onAnsweredRef.current?.(id);
          void settleFromServer(run, message, done);
        },
        onError: (frame) => {
          if (!current()) return;
          runRef.current = null;
          setStreaming(null);
          const failed: SendFailure = { code: 'unavailable', message: frame.message, retryable: true };
          setFailure({ threadId: id, value: failed });
          onFailureRef.current?.(failed, id);
        },
      }).catch((err: unknown) => {
        if (!current()) return;
        runRef.current = null;
        setStreaming(null);
        if (isAbortError(err)) return;
        const failed = toFailure(err);
        setFailure({ threadId: id, value: failed });
        if (DROP_PENDING.has(failed.code)) setPending(null);
        onFailureRef.current?.(failed, id);
      });
    },
    [abortRun, settleFromServer],
  );

  /**
   * Sends `text` to `target` (defaults to the open thread); returns false when
   * nothing was sent (no thread, or a send already in flight). An earlier
   * message still shown locally (after Stop or a failure) is kept in the list
   * as a plain user bubble rather than vanishing.
   */
  const send = useCallback(
    (text: string, options: { threadId?: number; about?: string } = {}): boolean => {
      const target = options.threadId ?? threadId;
      if (target == null || runRef.current) return false;
      if (pending && pending.threadId === target) {
        const kept = syntheticUserMessage(pending.text);
        setLoaded((prev) => ({
          threadId: target,
          disclaimer: prev?.threadId === target ? prev.disclaimer : '',
          messages: [...(prev?.threadId === target ? prev.messages : []), kept],
        }));
      }
      start({ threadId: target, text, about: options.about });
      return true;
    },
    [threadId, pending, start],
  );

  const retry = useCallback(() => {
    if (!pending || runRef.current) return;
    start(pending);
  }, [pending, start]);

  const stop = useCallback(() => {
    if (runRef.current?.settling) return;
    const run = abortRun();
    if (run) {
      setStreaming(null);
      setStoppedIn(run.threadId);
      return;
    }
    // Defensive: no run is current (no known path leaves `streaming` set
    // without one). Clear only the streaming bubble; `pending` is the parent's
    // question and every other terminal path keeps it for Retry.
    setStreaming((s) => (s?.threadId === threadId ? null : s));
  }, [abortRun, threadId]);

  const current = loaded && loaded.threadId === threadId ? loaded : null;
  const loadError = forThread(loadFailure, threadId);
  const streamingNow = forThread(streaming, threadId);

  return {
    messages: current?.messages ?? [],
    disclaimer: current?.disclaimer ?? '',
    loading: threadId != null && current === null && loadError === null,
    loadError,
    reload,
    pending: pending && pending.threadId === threadId ? pending : null,
    streaming: streamingNow,
    announcement: forThread(answered, threadId),
    failure: forThread(failure, threadId),
    stopped: threadId != null && stoppedIn === threadId,
    isStreaming: streamingNow !== null,
    send,
    retry,
    stop,
  };
}

/** Folds one `tool` frame into the activity rows: a start appends, a finish/fail closes the latest open row for that tool. */
function applyToolFrame(tools: ToolActivity[], frame: AdvocateToolFrame, nextKey: () => number): ToolActivity[] {
  if (frame.status === 'started') {
    return [...tools, { key: nextKey(), name: frame.name, label: frame.label, status: 'started' }];
  }
  for (let i = tools.length - 1; i >= 0; i -= 1) {
    if (tools[i].name === frame.name && tools[i].status === 'started') {
      const next = tools.slice();
      next[i] = { ...next[i], label: frame.label || next[i].label, status: frame.status };
      return next;
    }
  }
  // A finish without a start is still shown as a completed row.
  return [...tools, { key: nextKey(), name: frame.name, label: frame.label, status: frame.status }];
}
